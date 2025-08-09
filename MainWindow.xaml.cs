using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FlipRAR
{
  public partial class MainWindow : Window
  {
    private List<RarArchiveEntry> _pages = new List<RarArchiveEntry>();
    private int _index = -1;
    private RarArchive _archive;

    public MainWindow()
    {
      InitializeComponent();
    }

    // File > Open...
    private void Open_OnClick(object sender, RoutedEventArgs e) => ShowOpenDialog();

    private void Exit_OnClick(object sender, RoutedEventArgs e) => Close();

    private void PrevBtn_OnClick(object sender, RoutedEventArgs e) => GoTo(_index - 1);

    private void NextBtn_OnClick(object sender, RoutedEventArgs e) => GoTo(_index + 1);

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Left) { GoTo(_index - 1); e.Handled = true; }
      else if (e.Key == Key.Right) { GoTo(_index + 1); e.Handled = true; }
      else if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
      {
        ShowOpenDialog();
        e.Handled = true;
      }
    }

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
          .Where(e => !e.IsDirectory && exts.Contains(System.IO.Path.GetExtension(e.Key)))
          .OrderBy(e => e.Key, NaturalStringComparer.Instance) // ✅ natural sort fix
          .ToList();

      if (_pages.Count == 0)
        throw new InvalidDataException("No images were found inside this CBR.");
    }

    private void GoTo(int newIndex)
    {
      if (_pages == null || _pages.Count == 0) return;
      if (newIndex < 0 || newIndex >= _pages.Count) return;

      _index = newIndex;
      var entry = _pages[_index];

      if (entry.IsEncrypted)
      {
        MessageBox.Show(this, "This page is encrypted and can't be read.", "Encrypted",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      using (var ms = new MemoryStream())
      {
        entry.WriteTo(ms);
        ms.Position = 0;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad; // read fully so stream can close
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();

        ImageViewer.Source = bmp;
      }

      PrevBtn.IsEnabled = _index > 0;
      NextBtn.IsEnabled = _index < _pages.Count - 1;
      PageLabel.Text = $"Page {_index + 1} / {_pages.Count}";
    }

    private void ClearViewer()
    {
      ImageViewer.Source = null;
      _pages.Clear();
      _index = -1;
      PrevBtn.IsEnabled = NextBtn.IsEnabled = false;
      PageLabel.Text = "";
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
