using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FlipRAR
{
  public partial class MainWindow : Window
  {
    private List<RarArchiveEntry> _pages = new();
    private List<BitmapImage> _thumbnails = new();
    private int _index = -1;
    private RarArchive? _archive; // nullable until a file is opened

    public MainWindow()
    {
      InitializeComponent();
      PageInput.IsEnabled = false; // disabled until a CBR is loaded
    }

    // ===== Menu =====
    private void Open_OnClick(object sender, RoutedEventArgs e) => ShowOpenDialog();
    private void Exit_OnClick(object sender, RoutedEventArgs e) => Close();

    // ===== Toolbar =====
    private void FirstBtn_OnClick(object sender, RoutedEventArgs e) => GoTo(0);
    private void PrevBtn_OnClick(object sender, RoutedEventArgs e) => GoTo(_index - 1);
    private void NextBtn_OnClick(object sender, RoutedEventArgs e) => GoTo(_index + 1);
    private void LastBtn_OnClick(object sender, RoutedEventArgs e) => GoTo(_pages.Count - 1);

    // ===== Keyboard =====
    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
      if (_pages.Count > 0)
      {
        if (e.Key == Key.Left) { GoTo(_index - 1); e.Handled = true; }
        if (e.Key == Key.Right) { GoTo(_index + 1); e.Handled = true; }
      }
      if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
      {
        ShowOpenDialog();
        e.Handled = true;
      }
    }

    // ===== Page input =====
    private void PageInput_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key != Key.Enter) return;

      if (int.TryParse(PageInput.Text, out int pageNum))
      {
        int targetIndex = pageNum - 1;
        if (targetIndex >= 0 && targetIndex < _pages.Count)
        {
          GoTo(targetIndex);
          return;
        }
      }

      MessageBox.Show(this, "Page does not exist.", "Invalid Page",
          MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ===== Thumbnails =====
    private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (ThumbnailList.SelectedIndex >= 0 && ThumbnailList.SelectedIndex < _pages.Count)
        GoTo(ThumbnailList.SelectedIndex);
    }

    // ===== Open & load =====
    private void ShowOpenDialog()
    {
      var dlg = new OpenFileDialog
      {
        Title = "Open CBR",
        Filter = "Comic Book RAR (*.cbr)|*.cbr|All files (*.*)|*.*",
        CheckFileExists = true
      };
      if (dlg.ShowDialog() == true)
      {
        try
        {
          LoadCbr(dlg.FileName);
          GoTo(0);
        }
        catch (Exception ex)
        {
          MessageBox.Show(this, $"Failed to open CBR:\n{ex.Message}", "Error",
              MessageBoxButton.OK, MessageBoxImage.Error);
          ClearViewer();
        }
      }
    }

    private void LoadCbr(string path)
    {
      _archive?.Dispose();
      _archive = RarArchive.Open(path);

      var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

      _pages = _archive.Entries
          .Where(e => !e.IsDirectory && exts.Contains(Path.GetExtension(e.Key ?? string.Empty)))
          .OrderBy(e => e.Key, NaturalStringComparer.Instance) // natural sort
          .ToList();

      if (_pages.Count == 0)
        throw new InvalidDataException("No images were found inside this CBR.");

      // Build thumbnails
      _thumbnails.Clear();
      foreach (var entry in _pages)
      {
        using var ms = new MemoryStream();
        entry.WriteTo(ms);
        ms.Position = 0;

        var thumb = new BitmapImage();
        thumb.BeginInit();
        thumb.DecodePixelWidth = 120; // thumbnail width
        thumb.CacheOption = BitmapCacheOption.OnLoad;
        thumb.StreamSource = ms;
        thumb.EndInit();
        thumb.Freeze();

        _thumbnails.Add(thumb);
      }

      ThumbnailList.ItemsSource = _thumbnails;

      // Enable inputs now that a doc is loaded
      PageInput.IsEnabled = true;
      TotalPagesLabel.Text = $"/ {_pages.Count}";
    }

    // ===== Navigation & render =====
    private void GoTo(int newIndex)
    {
      if (_pages.Count == 0) return;
      if (newIndex < 0 || newIndex >= _pages.Count) return;

      _index = newIndex;
      var entry = _pages[_index];

      if (entry.IsEncrypted)
      {
        MessageBox.Show(this, "This page is encrypted and can't be read.", "Encrypted",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      using var ms = new MemoryStream();
      entry.WriteTo(ms);
      ms.Position = 0;

      var bmp = new BitmapImage();
      bmp.BeginInit();
      bmp.CacheOption = BitmapCacheOption.OnLoad;
      bmp.StreamSource = ms;
      bmp.EndInit();
      bmp.Freeze();

      ImageViewer.Source = bmp;

      // Update UI state
      FirstBtn.IsEnabled = PrevBtn.IsEnabled = _index > 0;
      LastBtn.IsEnabled = NextBtn.IsEnabled = _index < _pages.Count - 1;

      PageInput.Text = (_index + 1).ToString();
      PageInput.IsEnabled = true;
      TotalPagesLabel.Text = $"/ {_pages.Count}";

      // Sync thumbnails
      ThumbnailList.SelectedIndex = _index;
      ThumbnailList.ScrollIntoView(ThumbnailList.SelectedItem);
    }

    private void ClearViewer()
    {
      ImageViewer.Source = null;

      _pages.Clear();
      _thumbnails.Clear();
      ThumbnailList.ItemsSource = null;
      _index = -1;

      FirstBtn.IsEnabled = PrevBtn.IsEnabled = false;
      NextBtn.IsEnabled = LastBtn.IsEnabled = false;

      PageInput.Text = "";
      PageInput.IsEnabled = false;
      TotalPagesLabel.Text = "";

      _archive?.Dispose();
      _archive = null;
    }

    protected override void OnClosed(EventArgs e)
    {
      _archive?.Dispose();
      base.OnClosed(e);
    }
  }
}
