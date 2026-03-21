// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls
{
	public sealed partial class StatusBar : UserControl
	{
		public ICommandManager Commands { get; } = Ioc.Default.GetRequiredService<ICommandManager>();

		public StatusBarViewModel? StatusBarViewModel
		{
			get => (StatusBarViewModel)GetValue(StatusBarViewModelProperty);
			set => SetValue(StatusBarViewModelProperty, value);
		}

		// Using a DependencyProperty as the backing store for StatusBarViewModel.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty StatusBarViewModelProperty =
			DependencyProperty.Register(nameof(StatusBarViewModel), typeof(StatusBarViewModel), typeof(StatusBar), new PropertyMetadata(null));

		public SelectedItemsPropertiesViewModel? SelectedItemsPropertiesViewModel
		{
			get => (SelectedItemsPropertiesViewModel)GetValue(SelectedItemsPropertiesViewModelProperty);
			set => SetValue(SelectedItemsPropertiesViewModelProperty, value);
		}

		public static readonly DependencyProperty SelectedItemsPropertiesViewModelProperty =
			DependencyProperty.Register(nameof(SelectedItemsPropertiesViewModel), typeof(SelectedItemsPropertiesViewModel), typeof(StatusBar), new PropertyMetadata(null));

		public bool ShowInfoText
		{
			get => (bool)GetValue(ShowInfoTextProperty);
			set => SetValue(ShowInfoTextProperty, value);
		}

		// Using a DependencyProperty as the backing store for HideInfoText.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ShowInfoTextProperty =
			DependencyProperty.Register(nameof(ShowInfoText), typeof(bool), typeof(StatusBar), new PropertyMetadata(false));

		public StatusBar()
		{
			InitializeComponent();
		}

		private async void BranchesFlyout_Opening(object _, object e)
		{
			if (StatusBarViewModel is null)
				return;

			StatusBarViewModel.IsBranchesFlyoutExpanded = true;
			StatusBarViewModel.ShowLocals = true;
			await StatusBarViewModel.LoadBranches();
			StatusBarViewModel.SelectedBranchIndex = StatusBarViewModel.ACTIVE_BRANCH_INDEX;
		}

		private void BranchesList_ItemClick(object sender, ItemClickEventArgs e)
		{
			BranchesFlyout.Hide();
		}

		private void BranchesFlyout_Closing(object _, object e)
		{
			if (StatusBarViewModel is null)
				return;

			StatusBarViewModel.IsBranchesFlyoutExpanded = false;
		}

		private async void DeleteBranch_Click(object sender, RoutedEventArgs e)
		{
			if (StatusBarViewModel is null)
				return;

			BranchesFlyout.Hide();
			await StatusBarViewModel.ExecuteDeleteBranch(((BranchItem)((Button)sender).DataContext).Name);
		}

		// FolderDiag: per-drive disk space mini-bars in the status bar
		public void RefreshDriveSpaceBars()
		{
			if (DriveSpaceBarsPanel is null) return;
			DriveSpaceBarsPanel.Children.Clear();

			foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
			{
				double pct = drive.TotalSize > 0
					? 100.0 * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize
					: 0;

				var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
				chip.Children.Add(new TextBlock
				{
					Text = drive.Name.TrimEnd('\\'),
					FontSize = 11,
					VerticalAlignment = VerticalAlignment.Center,
					Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
				});

				var bar = new ProgressBar { Value = pct, Minimum = 0, Maximum = 100, Width = 60, Height = 6, VerticalAlignment = VerticalAlignment.Center };
				if (pct > 85)
					bar.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
				else if (pct > 70)
					bar.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
				chip.Children.Add(bar);

				chip.Children.Add(new TextBlock
				{
					Text = $"{pct:F0}%",
					FontSize = 10,
					VerticalAlignment = VerticalAlignment.Center,
					Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
				});

				DriveSpaceBarsPanel.Children.Add(chip);
			}
		}
		// End FolderDiag
	}
}
