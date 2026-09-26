using System;
using System.Runtime.InteropServices;

namespace OneExtend.Addin
{
    /// <summary>
    /// IDTExtensibility2 as consumed by Office COM add-in loaders, declared
    /// locally so no VSTO runtime or PIA dependency is required.
    /// </summary>
    [ComImport]
    [Guid("B65AD801-ABAF-11D0-BB8B-00A0C90F2744")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IDTExtensibility2
    {
        void OnConnection(
            [MarshalAs(UnmanagedType.IDispatch)] object application,
            ext_ConnectMode connectMode,
            [MarshalAs(UnmanagedType.IDispatch)] object addInInst,
            ref Array custom);

        void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom);
        void OnAddInsUpdate(ref Array custom);
        void OnStartupComplete(ref Array custom);
        void OnBeginShutdown(ref Array custom);
    }

    public enum ext_ConnectMode
    {
        ext_cm_AfterStartup = 0,
        ext_cm_Startup = 1,
        ext_cm_External = 2,
        ext_cm_CommandLine = 3,
        ext_cm_Solution = 4,
        ext_cm_StartupAfterOuterSolutionLoad = 5
    }

    public enum ext_DisconnectMode
    {
        ext_dm_HostShutdown = 0,
        ext_dm_UserClosed = 1,
        ext_dm_AdhocCommand = 2,
        ext_dm_SolutionClosed = 3,
        ext_dm_UIClosed = 4
    }
}
