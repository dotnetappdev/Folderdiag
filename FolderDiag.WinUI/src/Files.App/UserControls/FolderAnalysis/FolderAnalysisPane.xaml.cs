// Copyright (c) FolderDiag Project
// Licensed under the MIT License.

using Files.App.ViewModels.FolderAnalysis;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls.FolderAnalysis
{
    public sealed partial class FolderAnalysisPane : UserControl
    {
        public FolderAnalysisPaneViewModel ViewModel { get; } = new();

        public FolderAnalysisPane()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Called by the host page when the active folder path changes.
        /// Feeds the current path into the ViewModel.
        /// </summary>
        public void SetCurrentPath(string path)
        {
            ViewModel.CurrentPath = path;
        }
    }
}
