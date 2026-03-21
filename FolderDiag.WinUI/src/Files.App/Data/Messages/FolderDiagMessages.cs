// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Files.App.Data.Messages
{
    /// <summary>
    /// Sent when the user toggles the Folder Analysis pane from the toolbar.
    /// Value = true means open, false means close.
    /// </summary>
    public sealed class FolderDiagAnalysisPaneMessage : ValueChangedMessage<bool>
    {
        public FolderDiagAnalysisPaneMessage(bool isVisible) : base(isVisible) { }
    }
}
