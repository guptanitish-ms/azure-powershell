﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------
//

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Microsoft.Azure.PowerShell.Authenticators.Identity
{
    /// <summary>
    /// A cache for Tokens.
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    // SemaphoreSlim only needs to be disposed when AvailableWaitHandle is called.
    internal class TokenCache
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private DateTimeOffset _lastUpdated;
        private ConditionalWeakTable<object, CacheTimestamp> _cacheAccessMap;
        internal Func<IPublicClientApplication> _publicClientApplicationFactory;
        private readonly bool _allowUnencryptedStorage;
        private readonly string _name;
        private readonly bool _persistToDisk;
        private static AsyncLockWithValue<MsalCacheHelperWrapper> cacheHelperLock = new AsyncLockWithValue<MsalCacheHelperWrapper>();
        private readonly MsalCacheHelperWrapper _cacheHelperWrapper;

        /// <summary>
        /// The internal state of the cache.
        /// </summary>
        internal byte[] Data { get; private set; }

        private class CacheTimestamp
        {
            private DateTimeOffset _timestamp;

            public CacheTimestamp()
            {
                Update();
            }

            public DateTimeOffset Update()
            {
                _timestamp = DateTimeOffset.UtcNow;
                return _timestamp;
            }

            public DateTimeOffset Value { get { return _timestamp; } }
        }

        /// <summary>
        /// Creates a new instance of <see cref="TokenCache"/> with the specified options.
        /// </summary>
        /// <param name="options">Options controlling the storage of the <see cref="TokenCache"/>.</param>
        public TokenCache(TokenCachePersistenceOptions options = null)
            : this(options, default, default)
        { }

        internal TokenCache(TokenCachePersistenceOptions options, MsalCacheHelperWrapper cacheHelperWrapper, Func<IPublicClientApplication> publicApplicationFactory = null)
        {
            _cacheHelperWrapper = cacheHelperWrapper ?? new MsalCacheHelperWrapper();
            _publicClientApplicationFactory = publicApplicationFactory ?? new Func<IPublicClientApplication>(() => PublicClientApplicationBuilder.Create(Guid.NewGuid().ToString()).Build());
            if (options is UnsafeTokenCacheOptions inMemoryOptions)
            {
                TokenCacheUpdatedAsync = inMemoryOptions.TokenCacheUpdatedAsync;
                RefreshCacheFromOptionsAsync = inMemoryOptions.RefreshCacheAsync;
                _lastUpdated = DateTimeOffset.UtcNow;
                _cacheAccessMap = new ConditionalWeakTable<object, CacheTimestamp>();
            }
            else
            {
                _allowUnencryptedStorage = options?.UnsafeAllowUnencryptedStorage ?? false;
                _name = options?.Name ?? Constants.DefaultMsalTokenCacheName;
                _persistToDisk = true;
            }
        }

        /// <summary>
        /// A delegate that is called with the cache contents when the underlying <see cref="TokenCache"/> has been updated.
        /// </summary>
        internal Func<TokenCacheUpdatedArgs, Task> TokenCacheUpdatedAsync;

        /// <summary>
        /// A delegate that will be called before the cache is accessed. The data returned will be used to set the current state of the cache.
        /// </summary>
        internal Func<Task<ReadOnlyMemory<byte>>> RefreshCacheFromOptionsAsync;

        internal virtual async Task RegisterCache(bool async, ITokenCache tokenCache, CancellationToken cancellationToken)
        {
            if (_persistToDisk)
            {
                MsalCacheHelperWrapper cacheHelper = await GetCacheHelperAsync(async, cancellationToken).ConfigureAwait(false);
                cacheHelper.RegisterCache(tokenCache);
            }
            else
            {
                if (async)
                {
                    await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _lock.Wait(cancellationToken);
                }

                try
                {
                    if (!_cacheAccessMap.TryGetValue(tokenCache, out _))
                    {
                        tokenCache.SetBeforeAccessAsync(OnBeforeCacheAccessAsync);

                        tokenCache.SetAfterAccessAsync(OnAfterCacheAccessAsync);

                        _cacheAccessMap.Add(tokenCache, new CacheTimestamp());
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        /// <summary>
        /// Resets the <see cref="cacheHelperLock"/> so that tests can validate multiple calls to <see cref="RegisterCache"/>
        /// This should only be used for testing.
        /// </summary>
        internal static void ResetWrapperCache()
        {
            cacheHelperLock = new AsyncLockWithValue<MsalCacheHelperWrapper>();
        }

        private async Task OnBeforeCacheAccessAsync(TokenCacheNotificationArgs args)
        {
            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (RefreshCacheFromOptionsAsync != null)
                {
                    Data = (await RefreshCacheFromOptionsAsync().ConfigureAwait(false)).ToArray();
                }
                args.TokenCache.DeserializeMsalV3(Data, true);

                _cacheAccessMap.GetOrCreateValue(args.TokenCache).Update();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task OnAfterCacheAccessAsync(TokenCacheNotificationArgs args)
        {
            if (args.HasStateChanged)
            {
                await UpdateCacheDataAsync(args.TokenCache).ConfigureAwait(false);
            }
        }

        private async Task UpdateCacheDataAsync(ITokenCacheSerializer tokenCache)
        {
            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!_cacheAccessMap.TryGetValue(tokenCache, out CacheTimestamp lastRead) || lastRead.Value < _lastUpdated)
                {
                    Data = await MergeCacheData(Data, tokenCache.SerializeMsalV3()).ConfigureAwait(false);
                }
                else
                {
                    Data = tokenCache.SerializeMsalV3();
                }

                if (TokenCacheUpdatedAsync != null)
                {
                    var eventBytes = Data.ToArray();
                    await TokenCacheUpdatedAsync(new TokenCacheUpdatedArgs(eventBytes)).ConfigureAwait(false);
                }

                _lastUpdated = _cacheAccessMap.GetOrCreateValue(tokenCache).Update();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<byte[]> MergeCacheData(byte[] cacheA, byte[] cacheB)
        {
            byte[] merged = null;

            IPublicClientApplication client = _publicClientApplicationFactory();

            client.UserTokenCache.SetBeforeAccess(args => args.TokenCache.DeserializeMsalV3(cacheA));

            await client.GetAccountsAsync().ConfigureAwait(false);

            client.UserTokenCache.SetBeforeAccess(args => args.TokenCache.DeserializeMsalV3(cacheB, shouldClearExistingCache: false));

            client.UserTokenCache.SetAfterAccess(args => merged = args.TokenCache.SerializeMsalV3());

            await client.GetAccountsAsync().ConfigureAwait(false);

            return merged;
        }

        private async Task<MsalCacheHelperWrapper> GetCacheHelperAsync(bool async, CancellationToken cancellationToken)
        {
            using (var asyncLock = await cacheHelperLock.GetLockOrValueAsync(async, cancellationToken).ConfigureAwait(false))
            {
                if (asyncLock.HasValue)
                {
                    return asyncLock.Value;
                }

                MsalCacheHelperWrapper cacheHelper;

                try
                {
                    cacheHelper = await GetProtectedCacheHelperAsync(async, _name).ConfigureAwait(false);

                    cacheHelper.VerifyPersistence();
                }
                catch (MsalCachePersistenceException)
                {
                    if (_allowUnencryptedStorage)
                    {
                        cacheHelper = await GetFallbackCacheHelperAsync(async, _name).ConfigureAwait(false);

                        cacheHelper.VerifyPersistence();
                    }
                    else
                    {
                        throw;
                    }
                }

                asyncLock.SetValue(cacheHelper);

                return cacheHelper;
            }
        }

        private async Task<MsalCacheHelperWrapper> GetProtectedCacheHelperAsync(bool async, string name)
        {
            StorageCreationProperties storageProperties = new StorageCreationPropertiesBuilder(name, Constants.DefaultMsalTokenCacheDirectory)
                .WithMacKeyChain(Constants.DefaultMsalTokenCacheKeychainService, name)
                .WithLinuxKeyring(Constants.DefaultMsalTokenCacheKeyringSchema, Constants.DefaultMsalTokenCacheKeyringCollection, name, Constants.DefaultMsaltokenCacheKeyringAttribute1, Constants.DefaultMsaltokenCacheKeyringAttribute2)
                .Build();

            MsalCacheHelperWrapper cacheHelper = await InitializeCacheHelper(async, storageProperties).ConfigureAwait(false);

            return cacheHelper;
        }

        private async Task<MsalCacheHelperWrapper> GetFallbackCacheHelperAsync(bool async, string name = Constants.DefaultMsalTokenCacheName)
        {
            StorageCreationProperties storageProperties = new StorageCreationPropertiesBuilder(name, Constants.DefaultMsalTokenCacheDirectory)
                .WithMacKeyChain(Constants.DefaultMsalTokenCacheKeychainService, name)
                .WithLinuxUnprotectedFile()
                .Build();

            MsalCacheHelperWrapper cacheHelper = await InitializeCacheHelper(async, storageProperties).ConfigureAwait(false);

            return cacheHelper;
        }

        private async Task<MsalCacheHelperWrapper> InitializeCacheHelper(bool async, StorageCreationProperties storageProperties)
        {
            if (async)
            {
                await _cacheHelperWrapper.InitializeAsync(storageProperties).ConfigureAwait(false);
            }
            else
            {
#pragma warning disable AZC0102 // Do not use GetAwaiter().GetResult(). Use the TaskExtensions.EnsureCompleted() extension method instead.
                _cacheHelperWrapper.InitializeAsync(storageProperties).GetAwaiter().GetResult();
#pragma warning restore AZC0102 // Do not use GetAwaiter().GetResult(). Use the TaskExtensions.EnsureCompleted() extension method instead.
            }
            return _cacheHelperWrapper;
        }
    }
}
