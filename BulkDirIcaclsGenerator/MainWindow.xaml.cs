using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BulkDirIcaclsGenerator
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Możliwe uprawnienia do wyboru
        private readonly string[] Permissions = { "RX", "M", "F", "R", "W" };

        // Dane folderów
        private List<TableData> Data;


        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                // Wyświetla okno
                folderBrowserDialog.ShowDialog();

                // Czyści pole tekstowe
                OutputTextBox.Text = string.Empty;

                // Pobiera listę folderów
                List<DirectoryInfo> directoryInfos = this.GetDirectoryInfos(folderBrowserDialog.SelectedPath);

                // Czyści dane tabeli
                Data = new List<TableData>();

                // DLa każdego folderu dodaje wiersz tabeli
                foreach (DirectoryInfo info in directoryInfos)
                    Data.Add(new TableData(info));

                // Ustawia źródło danych dla tabeli xaml
                MainDataGrid.ItemsSource = Data;

                // Uaktywnia przycisk zapisu wyjścia do pliku
                SaveOutputMenuItem.IsEnabled = true;

                // Uaktywnia przycisk wykonania komend
                ExecuteButton.IsEnabled = true;
            }


            // Jeśli wcześniej były wczytane grupy to rezerwuje miejsce w pamięci na informacje o uprawnieniach
            foreach (var tableData in Data) // Dla każdego wiersza
            {
                // Dla każdej kolumny z grupą
                for (int i = 1; i < this.MainDataGrid.Columns.Count; ++i)
                    tableData.addColumn();  // Dodaje pole na uprawnienie
            }
        }


        private List<DirectoryInfo> GetDirectoryInfos(string path)
        {
            // Tworzy pustą listę na informacje o folderach
            List<DirectoryInfo> directoryInfoList = new List<DirectoryInfo>();

            // Pobiera listę podfolderów
            foreach (DirectoryInfo directory in new DirectoryInfo(path).GetDirectories())
            {
                try
                {
                    // Dodaje folder do listy
                    directoryInfoList.Add(directory);

                    // Jeśli folder zawiera podfoldery
                    if (directory.GetDirectories().Length > 0)
                        // Wykonuje rekurencyjnie tą samą funkcję i łączy listy folderów
                        directoryInfoList.AddRange(GetDirectoryInfos(directory.FullName));
                }
                // Gdy wystąpił problem z dostępem
                catch (UnauthorizedAccessException ex)
                {
                    // Wyświetla informacje w polu tekstowym
                    OutputTextBox.Text += ex.Message + "\n";
                }
            }
            return directoryInfoList;
        }

        private void AddGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            // Jeśli nie wprowadzono danych to kończy funkcję
            if (AddGroupTextBox.Text == string.Empty)
                return;

            // Jeśli dane folderów nie są puste
            if (this.Data != null)
            {
                // Wywołuje funkcję dodawanie grup
                AddGroups(AddGroupTextBox.Text.Split(';'));
            }
            else // Jeśli nie wczytano folderów
            {
                // Wyświetla informację
                System.Windows.MessageBox.Show("Load folder before adding groups");
            }
        }

        private void AddGroups(string[] groupNames)
        {
            // Czyści wyjściowe pole tekstowe
            OutputTextBox.Text = String.Empty;
            
            // Zmienne do przechowywania połączenia
            PrincipalContext context;
            try
            {
                // Próba połączenia z AD
                context = new PrincipalContext(ContextType.Domain);
            }
            catch (PrincipalServerDownException) // Jeśli nie udało się połączyć z AD
            {
                // Łączy z lokalnym komputerem
                context = new PrincipalContext(ContextType.Machine);
            }

            // Dla każdej podanej grupy
            foreach (string groupName in groupNames)
            {
                // Odnajduje grupę po nazwie
                GroupPrincipal byIdentity = GroupPrincipal.FindByIdentity(context, groupName);

                // Jeśli udało się odnaleźć
                if (byIdentity != null)
                {
                    // Dodaje do każdego wiersza pole w kolumnie dla danej grupy
                    foreach (var tableData in Data)
                        tableData.addColumn();


                    // Pobiera kolumny z xaml
                    var columns = MainDataGrid.Columns;

                    // Tworzy tam nową kolumnę i przypisuje jej wartości
                    DataGridComboBoxColumn gridComboBoxColumn = new DataGridComboBoxColumn()
                    {
                        Header = byIdentity.Name,       // Nazwa
                        ItemsSource = Permissions,      // Lista do wyboru
                        // Przypisanie do pola w pamięci
                        SelectedItemBinding = new System.Windows.Data.Binding("Permissions[" + (MainDataGrid.Columns.Count - 1) + "]")
                    };

                    // Dopisuje nową kolumnę do XAML
                    columns.Add(gridComboBoxColumn);
                }
                else
                {
                // Nie udało się odnaleźć grupy
                }
                {
                    OutputTextBox.Text += "Could not find group " + groupName + "\n";
                }
            }
        }


        private void PermissionHelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Wyświetla pomoc (help -> permissions)
            System.Windows.MessageBox.Show("F - full access\nM - modify access\nRX - read and execute access\nR - read-only access\nW - write-only access", "Simple rights");
        }

        private void GenerateIcaclsButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateIcacls();
        }


        private void GenerateIcacls()
        {
            // Jeśli nie wczytano folderów to kończy funkcję
            if (Data == null)
                return;

            // Czyści wyjściowe pole tekstowe
            OutputTextBox.Text = string.Empty;


            string str1 = string.Empty;

            // Dobiera odpowiednie uprawnienia według ustawień użytkownika
            if (!NPInheritMenuItem.IsChecked)
                str1 = (OIInheritMenuItem.IsChecked ? "(OI)" : "") + (CIInheritMenuItem.IsChecked ? "(CI)" : "") + (IOInheritMenuItem.IsChecked ? "(IO)" : "");


            foreach (TableData tableData in Data)
            {

                string str2 = "icacls \"" + tableData.FullName + "\" ";

                // Ustawienia dziedziczenia uprawnień - według ustawień użytkownika
                if (EnableInheritanceMenuItem.IsChecked)
                    str2 += "/inheritance:e ";
                else if (DisableInheritanceMenuItem.IsChecked)
                    str2 += "/inheritance:d ";
                else if (RemoveInheritanceMenuItem.IsChecked)
                    str2 += "/inheritance:r ";

                // Dla każdej kolumny z grupą
                for (int i = 1; i < this.MainDataGrid.Columns.Count; ++i)
                {
                    // Odczytuje uprawnienia wybrane przez użytownika
                    string permission = tableData.Permissions[i - 1];

                    // Jeśli są przypisane
                    if (permission != string.Empty)
                        // Tworzy komendę icacls
                        str2 += "/grant:r \"" + this.MainDataGrid.Columns[i].Header + "\":" + str1 + "(" + permission + ") ";
                }
                // Dodaje pełną komendę do wyjściowego pola tekstowego
                OutputTextBox.Text += str2 + Environment.NewLine;
            }
        }

        private void NPInheritMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Odznacza wszystkie inne opcje w tej kategorii
            this.OIInheritMenuItem.IsChecked = false;
            this.CIInheritMenuItem.IsChecked = false;
            this.IOInheritMenuItem.IsChecked = false;
        }


        private void UncheckNPInheritMenuItem(object sender, RoutedEventArgs e)
        {
            // Odznacza kolidująca opcję
            this.NPInheritMenuItem.IsChecked = false;
        }


        private void InherintaceHelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Wyświetla pomoc (help -> inheritance)
            System.Windows.MessageBox.Show("(OI) + (CI) - The folder, subfolders and files\n(CI) - The folder and subfolders\n(OI) - The folder and files\n(OI) + (CI) + (IO) - Subfolders and files only\n(CI) + (IO) - Subfolders only\n(OI) + (IO) - Files only", "Inheritance rights");
        }


        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            // Generuje komendy
            GenerateIcacls();

            //Dzieli wyjściowe pole tekstowe na linie
            foreach (string str in OutputTextBox.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
            {
                try
                {
                    // Tworzy nowy proces
                    new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            // Ukrywa okno procesu
                            WindowStyle = ProcessWindowStyle.Hidden,
                            // Uruchamia cmd
                            FileName = "cmd.exe",
                            // Z komendą icacls
                            Arguments = ("/C " + str)
                        }
                    }.Start();  // Wywołuje proces
                }
                catch (Exception)   // Jeśli wystąpił jakiś błąd
                {
                    // Wyświetla informację w wyjściowym polu tekstowym
                    OutputTextBox.Text = "Unexpected error in: " + str;
                }
            }
            // Wyświetla okno z informacją o zakończeniu działań
            System.Windows.MessageBox.Show("Icacls commands executed", "Complete");
        }

        private void SaveOutputMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Tworzy okno dialogowe do zapisu pliku
            SaveFileDialog saveFileDialog1 = new SaveFileDialog()
            {
                // Domyślne rozszerzenie zapisywanego pliku
                DefaultExt = ".txt",
                // Dodawaj rozszerzenie jeśli użytkownik nie wpisze własnego
                AddExtension = true,
                // Domyślne wyświetlane pliki w eksploratorze
                Filter = "Normal text file (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            using (saveFileDialog1)
            {
                // Wyświetla okno
                saveFileDialog1.ShowDialog();

                // Jeśli nie wybrano poprawnie pliku to kończy funkcję
                if (saveFileDialog1.FileName == string.Empty)
                    return;


                using (StreamWriter streamWriter = new StreamWriter(saveFileDialog1.FileName))
                {

                    // Dzieli pole wyjściowe pole tekstowe na linie
                    foreach (string str in OutputTextBox.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                        // Zapisuje każdą linię do pliku
                        streamWriter.WriteLine(str);

                    // Zamyka plik
                    streamWriter.Close();
                }
            }
        }



        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Zamyka aplikację
            System.Windows.Application.Current.Shutdown();
        }

        private void EnableInheritanceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Odznacza koliduje opcje
            this.DisableInheritanceMenuItem.IsChecked = false;
            this.RemoveInheritanceMenuItem.IsChecked = false;
        }

        private void DisableInheritanceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Odznacza koliduje opcje
            this.EnableInheritanceMenuItem.IsChecked = false;
            this.RemoveInheritanceMenuItem.IsChecked = false;
        }

        private void RemoveInheritanceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Odznacza koliduje opcje
            this.EnableInheritanceMenuItem.IsChecked = false;
            this.DisableInheritanceMenuItem.IsChecked = false;
        }


    }
}
