//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PhoneBox.Abstractions
{
    [global::System.CodeDom.Compiler.GeneratedCode("PhoneBox.Generators", "%GENERATORVERSION%")]
    public interface ITelephonyHub
    {
        global::System.Threading.Tasks.Task ReceiveCallConnected(global::PhoneBox.Abstractions.CallConnectedEvent content);
        global::System.Threading.Tasks.Task ReceiveCallDisconnected(global::PhoneBox.Abstractions.CallDisconnectedEvent content);
    }
}