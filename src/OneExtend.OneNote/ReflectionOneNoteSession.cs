using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OneExtend.OneNote
{
    /// <summary>
    /// Late-bound (reflection) wrapper around the OneNote COM application object
    /// (ProgID "OneNote.Application"). Late binding avoids a hard dependency on
    /// the Office PIA so the assembly builds and loads on any machine with OneNote
    /// desktop installed.
    /// </summary>
    public sealed class ReflectionOneNoteSession : IOneNoteSession
    {
        private object _app;
        private bool _disposed;

        private const int PiPageInfoDefault = 1;          // pageInfoDefault
        private const int XmlSchema2013 = 2;              // xs2013

        public bool IsConnected => _app != null;

        public ReflectionOneNoteSession()
        {
            try
            {
                var type = Type.GetTypeFromProgID("OneNote.Application");
                if (type != null)
                    _app = Activator.CreateInstance(type);
            }
            catch (COMException)
            {
                _app = null;
            }
        }

        public string GetActivePageId()
        {
            var app = RequireApp();
            var windows = GetProperty(app, "Windows");
            var count = Convert.ToInt32(GetProperty(windows, "Count"), CultureInfo());
            for (var i = 0; i < count; i++)
            {
                // IWindows.Item(i) is 0-based via IDispatch on some versions, 1-based on others;
                // try 0-based first, fall back to 1-based.
                object window = null;
                try { window = Invoke(windows, "Item", new object[] { i }); }
                catch (COMException) { window = Invoke(windows, "Item", new object[] { i + 1 }); }
                if (window == null)
                    continue;

                object active;
                try { active = GetProperty(window, "Active"); }
                catch (COMException) { continue; }
                if (active is bool b && b)
                {
                    var pageId = GetProperty(window, "CurrentPageId") as string;
                    if (!string.IsNullOrEmpty(pageId))
                        return pageId;
                }
            }
            return null;
        }

        public string GetPageContent(string pageId)
        {
            if (string.IsNullOrEmpty(pageId))
                throw new ArgumentException("Page id is required.", nameof(pageId));
            var app = RequireApp();
            var args = new object[] { pageId, null, PiPageInfoDefault, XmlSchema2013 };
            var flags = BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding;
            _ = app.GetType().InvokeMember(
                "GetPageContent", flags, null, app, args, CultureInfo());
            return args[1] as string;
        }

        public void UpdatePageContent(string pageXml)
        {
            if (string.IsNullOrEmpty(pageXml))
                throw new ArgumentException("Page XML is required.", nameof(pageXml));
            var app = RequireApp();
            var args = new object[] { pageXml, DateTime.MinValue.ToUniversalTime(), XmlSchema2013 };
            var flags = BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding;
            _ = app.GetType().InvokeMember(
                "UpdatePageContent", flags, null, app, args, CultureInfo());
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_app != null && Marshal.IsComObject(_app))
            {
                try { Marshal.ReleaseComObject(_app); } catch { /* already released */ }
            }
            _app = null;
        }

        private object RequireApp()
        {
            if (_app == null)
                throw new InvalidOperationException(
                    "OneNote application object is not available (is OneNote desktop running?).");
            return _app;
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty | BindingFlags.InvokeMethod,
                null, target, null, CultureInfo());
        }

        private static object Invoke(object target, string name, object[] args)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.InvokeMethod | BindingFlags.GetProperty,
                null, target, args, CultureInfo());
        }

        private static System.Globalization.CultureInfo CultureInfo() =>
            System.Globalization.CultureInfo.InvariantCulture;
    }
}
