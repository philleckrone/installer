using System;
using System.Text;
using System.Runtime.InteropServices;
public static class LvText {
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, IntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, IntPtr size, uint type);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, IntPtr n, out IntPtr done);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, IntPtr n, out IntPtr done);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr p, out bool wow);

    public static int Count(IntPtr lv) { return (int)SendMessage(lv, 0x1004, IntPtr.Zero, IntPtr.Zero); }
    public static bool Ticked(IntPtr lv, int i) { return (((int)SendMessage(lv, 0x102C, (IntPtr)i, (IntPtr)0xF000)) >> 12) == 2; }

    /// <summary>The text of a row (column 0) of a list in another 64-bit process (LVM_GETITEMTEXTW).</summary>
    public static string ItemText(IntPtr lv, int i) {
        uint pid; GetWindowThreadProcessId(lv, out pid);
        IntPtr p = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);
        if (p == IntPtr.Zero) throw new Exception("OpenProcess failed");
        const int head = 96, chars = 260;
        IntPtr mem = VirtualAllocEx(p, IntPtr.Zero, (IntPtr)(head + chars * 2), 0x3000, 0x04);
        try {
            byte[] b = new byte[head];
            BitConverter.GetBytes(0x0001).CopyTo(b, 0);                         // mask = LVIF_TEXT
            BitConverter.GetBytes(i).CopyTo(b, 4);
            bool wow; IsWow64Process(p, out wow);   // a 32-bit target uses the 32-bit LVITEM
            if (wow) {
                BitConverter.GetBytes((int)(((long)mem) + head)).CopyTo(b, 20); // pszText
                BitConverter.GetBytes(chars).CopyTo(b, 24);                     // cchTextMax
            } else {
                BitConverter.GetBytes(((long)mem) + head).CopyTo(b, 24);
                BitConverter.GetBytes(chars).CopyTo(b, 32);
            }
            IntPtr done;
            WriteProcessMemory(p, mem, b, (IntPtr)b.Length, out done);
            int n = (int)SendMessage(lv, 0x1073, (IntPtr)i, mem);
            byte[] t = new byte[chars * 2];
            ReadProcessMemory(p, (IntPtr)(((long)mem) + head), t, (IntPtr)t.Length, out done);
            return Encoding.Unicode.GetString(t, 0, Math.Max(0, n) * 2);
        } finally { VirtualFreeEx(p, mem, IntPtr.Zero, 0x8000); CloseHandle(p); }
    }

    /// <summary>A row's rectangle in the list's client coordinates (LVM_GETITEMRECT, LVIR_BOUNDS):
    /// left, top, right, bottom. A RECT has the same layout in 32- and 64-bit processes.</summary>
    public static int[] ItemRect(IntPtr lv, int i) {
        uint pid; GetWindowThreadProcessId(lv, out pid);
        IntPtr p = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);
        IntPtr mem = VirtualAllocEx(p, IntPtr.Zero, (IntPtr)16, 0x3000, 0x04);
        try {
            byte[] b = new byte[16];   // left = LVIR_BOUNDS (0)
            IntPtr done;
            WriteProcessMemory(p, mem, b, (IntPtr)16, out done);
            SendMessage(lv, 0x100E, (IntPtr)i, mem);
            ReadProcessMemory(p, mem, b, (IntPtr)16, out done);
            return new[] { BitConverter.ToInt32(b, 0), BitConverter.ToInt32(b, 4), BitConverter.ToInt32(b, 8), BitConverter.ToInt32(b, 12) };
        } finally { VirtualFreeEx(p, mem, IntPtr.Zero, 0x8000); CloseHandle(p); }
    }

    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    static IntPtr XY(int x, int y) { return (IntPtr)((y << 16) | (x & 0xFFFF)); }
    /// <summary>A left click at a client point, as posted mouse messages (the real mouse does not move).</summary>
    public static void Click(IntPtr h, int x, int y) {
        PostMessage(h, 0x0201, (IntPtr)1, XY(x, y));   // WM_LBUTTONDOWN
        PostMessage(h, 0x0202, IntPtr.Zero, XY(x, y)); // WM_LBUTTONUP
    }
    /// <summary>A double-click at a client point, as Windows sends it: down, up, double-click, up.</summary>
    public static void DoubleClick(IntPtr h, int x, int y) {
        Click(h, x, y);
        PostMessage(h, 0x0203, (IntPtr)1, XY(x, y));   // WM_LBUTTONDBLCLK
        PostMessage(h, 0x0202, IntPtr.Zero, XY(x, y));
    }

    /// <summary>Selects and focuses a row (LVM_SETITEMSTATE, LVIS_SELECTED | LVIS_FOCUSED).</summary>
    public static void Select(IntPtr lv, int i) {
        uint pid; GetWindowThreadProcessId(lv, out pid);
        IntPtr p = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);
        IntPtr mem = VirtualAllocEx(p, IntPtr.Zero, (IntPtr)96, 0x3000, 0x04);
        try {
            byte[] b = new byte[96];
            BitConverter.GetBytes(0x0008).CopyTo(b, 0);
            BitConverter.GetBytes(0x3).CopyTo(b, 12);
            BitConverter.GetBytes(0x3).CopyTo(b, 16);
            IntPtr done;
            WriteProcessMemory(p, mem, b, (IntPtr)b.Length, out done);
            SendMessage(lv, 0x102B, (IntPtr)i, mem);
        } finally { VirtualFreeEx(p, mem, IntPtr.Zero, 0x8000); CloseHandle(p); }
    }

    /// <summary>Ticks a row (LVM_SETITEMSTATE with the checked state image).</summary>
    public static void Tick(IntPtr lv, int i, bool on) {
        uint pid; GetWindowThreadProcessId(lv, out pid);
        IntPtr p = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);
        IntPtr mem = VirtualAllocEx(p, IntPtr.Zero, (IntPtr)96, 0x3000, 0x04);
        try {
            byte[] b = new byte[96];
            BitConverter.GetBytes(0x0008).CopyTo(b, 0);
            BitConverter.GetBytes(on ? 0x2000 : 0x1000).CopyTo(b, 12);
            BitConverter.GetBytes(0xF000).CopyTo(b, 16);
            IntPtr done;
            WriteProcessMemory(p, mem, b, (IntPtr)b.Length, out done);
            SendMessage(lv, 0x102B, (IntPtr)i, mem);
        } finally { VirtualFreeEx(p, mem, IntPtr.Zero, 0x8000); CloseHandle(p); }
    }
}
