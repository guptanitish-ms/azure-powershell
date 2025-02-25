// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is regenerated.

namespace Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview
{
    using static Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Runtime.Extensions;

    /// <summary>
    /// The secret info when type is rawValue. It's for scenarios that user input the secret.
    /// </summary>
    public partial class ValueSecretInfo :
        Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.IValueSecretInfo,
        Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.IValueSecretInfoInternal,
        Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Runtime.IValidates
    {
        /// <summary>
        /// Backing field for Inherited model <see cref= "Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.ISecretInfoBase"
        /// />
        /// </summary>
        private Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.ISecretInfoBase __secretInfoBase = new Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.SecretInfoBase();

        /// <summary>The secret type.</summary>
        [Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Origin(Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.PropertyOrigin.Inherited)]
        public Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Support.SecretType SecretType { get => ((Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.ISecretInfoBaseInternal)__secretInfoBase).SecretType; set => ((Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.ISecretInfoBaseInternal)__secretInfoBase).SecretType = value ; }

        /// <summary>Backing field for <see cref="Value" /> property.</summary>
        private string _value;

        /// <summary>The actual value of the secret.</summary>
        [Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Origin(Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.PropertyOrigin.Owned)]
        public string Value { get => this._value; set => this._value = value; }

        /// <summary>Validates that this object meets the validation criteria.</summary>
        /// <param name="eventListener">an <see cref="Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Runtime.IEventListener" /> instance that will receive validation
        /// events.</param>
        /// <returns>
        /// A <see cref = "global::System.Threading.Tasks.Task" /> that will be complete when validation is completed.
        /// </returns>
        public async global::System.Threading.Tasks.Task Validate(Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Runtime.IEventListener eventListener)
        {
            await eventListener.AssertNotNull(nameof(__secretInfoBase), __secretInfoBase);
            await eventListener.AssertObjectIsValid(nameof(__secretInfoBase), __secretInfoBase);
        }

        /// <summary>Creates an new <see cref="ValueSecretInfo" /> instance.</summary>
        public ValueSecretInfo()
        {

        }
    }
    /// The secret info when type is rawValue. It's for scenarios that user input the secret.
    public partial interface IValueSecretInfo :
        Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Runtime.IJsonSerializable,
        Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.ISecretInfoBase
    {
        /// <summary>The actual value of the secret.</summary>
        [Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Runtime.Info(
        Required = false,
        ReadOnly = false,
        Description = @"The actual value of the secret.",
        SerializedName = @"value",
        PossibleTypes = new [] { typeof(string) })]
        string Value { get; set; }

    }
    /// The secret info when type is rawValue. It's for scenarios that user input the secret.
    internal partial interface IValueSecretInfoInternal :
        Microsoft.Azure.PowerShell.Cmdlets.ServiceLinker.Models.Api20221101Preview.ISecretInfoBaseInternal
    {
        /// <summary>The actual value of the secret.</summary>
        string Value { get; set; }

    }
}