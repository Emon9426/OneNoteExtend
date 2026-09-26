using System;

namespace OneExtend.OneNote
{
    /// <summary>The subset of the OneNote application API the add-in needs.</summary>
    public interface IOneNoteSession : IDisposable
    {
        bool IsConnected { get; }

        /// <summary>Page id of the currently active window's page, or null.</summary>
        string GetActivePageId();

        /// <summary>Full page XML for the given page id.</summary>
        string GetPageContent(string pageId);

        /// <summary>Writes modified page XML back to the page.</summary>
        void UpdatePageContent(string pageXml);
    }
}
