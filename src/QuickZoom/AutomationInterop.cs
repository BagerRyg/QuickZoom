using System;
using System.Runtime.InteropServices;

namespace QuickZoom;

// Narrow, read-only projections of the Windows SDK UIAutomationClient.h
// interfaces. CLR _VtblGap methods preserve the omitted native slots. In
// particular no text-value, SetFocus, Select, or ScrollIntoView API is exposed.
internal static class AutomationInterop
{
    [ComImport, Guid("34723aff-0c9d-49d0-9896-7ab52df8cd8a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClient // IUIAutomation2 (55 base slots)
    {
        void _VtblGap1_5();
        IElement GetFocusedElement();
        void _VtblGap2_51();
        uint GetConnectionTimeout();
        void SetConnectionTimeout(uint milliseconds);
        uint GetTransactionTimeout();
        void SetTransactionTimeout(uint milliseconds);
    }

    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IElement
    {
        void _VtblGap1_1();
        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)] int[] GetRuntimeId();
        void _VtblGap2_5();
        [return: MarshalAs(UnmanagedType.Struct)] object GetCurrentPropertyValue(int property);
        void _VtblGap3_5();
        [return: MarshalAs(UnmanagedType.IUnknown)] object? GetCurrentPattern(int pattern);
    }

    [ComImport, Guid("506a921a-fcc9-409f-b23b-37eb74106872"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITextPattern2
    {
        void _VtblGap1_7();
        ITextRange? GetCaretRange([MarshalAs(UnmanagedType.Bool)] out bool active);
    }

    [ComImport, Guid("32eba289-3583-42c9-9c59-3b6d9a1e9b6a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITextPattern
    {
        void _VtblGap1_2();
        IRangeArray? GetSelection();
    }

    [ComImport, Guid("ce4ae76a-e717-4c98-81ea-47371d028eb6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IRangeArray
    {
        int GetLength();
        ITextRange GetElement(int index);
    }

    [ComImport, Guid("a543cc6a-f4ae-494b-8239-c814481187a8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITextRange
    {
        ITextRange Clone();
        void _VtblGap1_1();
        int CompareEndpoints(int endpoint, ITextRange other, int otherEndpoint);
        void ExpandToEnclosingUnit(int unit);
        void _VtblGap2_3();
        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_R8)] double[] GetBoundingRectangles();
        void _VtblGap3_4();
        void MoveEndpointByRange(int endpoint, ITextRange other, int otherEndpoint);
    }
}
