﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    [global::System.CodeDom.Compiler.GeneratedCode("PhoneBox.Generators", "%GENERATORVERSION%")]
    [global::System.Diagnostics.DebuggerNonUserCode]
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static class HubExtensions
    {
        public static global::Microsoft.AspNetCore.Builder.HubEndpointConventionBuilder MapGeneratedHub<THub>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints) where THub : global::PhoneBox.Generators.Tests.SignalR.TelephonyHub
        {
            return global::Microsoft.AspNetCore.Builder.HubEndpointRouteBuilderExtensions.MapHub<THub>(endpoints, "/TelephonyHub");
        }
    }
}