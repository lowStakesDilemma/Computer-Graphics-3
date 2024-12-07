using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace GK_Project_3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public class ImageFile
    {
        public string FileName { get; set; }
        public ImageFile(string fileName)
        {
            FileName = fileName;
        }
    }

    public partial class MainWindow : Window
    {
        private static string[] _filters = { ".jpg", ".jpeg"};
        public static readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPEG", ".JPE", ".BMP", ".GIF", ".PNG" };
        public WriteableBitmap WriteableBitmapPOU { get; set; }
        public WriteableBitmap WriteableBitmapPA {  get; set; }

        public WriteableBitmap WriteableBitmapKM { get; set; }

        private bool[] _enumHelper = {true, false, false};
        public bool[] EnumHelper
        {
            get
            {
                return _enumHelper;
            }
        }
        public enum POUMatrix
        {
            FLOYD_STEINBERG,
            BURKE,
            STUCKY
        };
        public POUMatrix PropagationOfUncertaintyMatrix
        {
            get
            {
                return (POUMatrix)(Array.IndexOf(EnumHelper, true));
            }
        }
        public ObservableCollection<ImageFile> ImageFiles { get; set; } = [];
        public int NumberOfColors { get; set; } = 12;

        public int Epsilon { get; set; } = 10;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            getImageList();
        }

        private void getImageList()
        {
            List<string> fileNames = [];
            var folderDialog = new OpenFolderDialog();
            if (folderDialog.ShowDialog() == true)
            {
                var folder = folderDialog.FolderName;
                var files = Directory.GetFiles(folder);
                foreach (var file in files)
                {
                    if (ImageExtensions.Contains(System.IO.Path.GetExtension(file).ToUpperInvariant()))
                    {
                        ImageFiles.Add(new(file));
                    }
                }
            }
        }

        private void ClusterImageButton_Click(object sender, RoutedEventArgs e)
        {
            if(imageList.SelectedItem == null)
            {
                MessageBox.Show("Please select an image to reduce.");
                return;
            }
            WriteableBitmapPOU = PropagationOfUncertainty.Dithering(new((imageList.SelectedItem as ImageFile).FileName), NumberOfColors, PropagationOfUncertaintyMatrix);
            WriteableBitmapPA = PropagationOfUncertainty.Dithering(new((imageList.SelectedItem as ImageFile).FileName), NumberOfColors);
            WriteableBitmapKM = PropagationOfUncertainty.Dithering(new((imageList.SelectedItem as ImageFile).FileName), NumberOfColors, Epsilon);
            POUImage.Source = WriteableBitmapPOU;
            PAImage.Source = WriteableBitmapPA;
            KMImage.Source = WriteableBitmapKM;
            POUImage.InvalidateVisual();
            PAImage.InvalidateVisual();
            KMImage.InvalidateVisual();
        }

        private void ChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Image files | *.jpg;*.jpeg";
            if (fileDialog.ShowDialog() == true)
            {
                ImageFiles.Clear();
                ImageFiles.Add(new(fileDialog.FileName));
                var folder = System.IO.Path.GetDirectoryName(fileDialog.FileName);
                var files = Directory.GetFiles(folder);
                foreach (var file in files)
                {
                    if (file != fileDialog.FileName && ImageExtensions.Contains(System.IO.Path.GetExtension(file).ToUpperInvariant()))
                    {
                        ImageFiles.Add(new(file));
                    }
                }
                imageList.SelectedIndex = 0;
            }
        }

        private void CreateImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (imageList.SelectedItem == null)
            {
                MessageBox.Show("Please select an image to reduce.");
                return;
            }
            WriteableBitmapPOU = PropagationOfUncertainty.Dithering(new((imageList.SelectedItem as ImageFile).FileName), NumberOfColors, PropagationOfUncertaintyMatrix);
            WriteableBitmapKM = PropagationOfUncertainty.Dithering(new ((imageList.SelectedItem as ImageFile).FileName), NumberOfColors, Epsilon);
            POUImage.Source = WriteableBitmapPOU;
            KMImage.Source = WriteableBitmapKM;
            POUImage.InvalidateVisual();
            KMImage.InvalidateVisual();
        }
    }
}