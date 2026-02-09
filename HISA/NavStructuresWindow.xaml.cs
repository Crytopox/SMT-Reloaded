using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EVEDataUtils;
using HISA.EVEData;

namespace HISA
{
    public partial class NavStructuresWindow : Window
    {
        public HISA.EVEData.EveManager EM { get; set; }

        public NavStructuresWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (EM == null)
            {
                return;
            }

            JumpBridgeList.ItemsSource = EM.JumpBridges;
            UpdateJumpBridgeSummary();
            ApplyJumpBridgeFilter();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveJumpGatesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (EM == null)
            {
                return;
            }

            string jbFileName = Path.Combine(EM.SaveDataRootFolder, "JumpBridges_" + JumpBridge.SaveVersion + ".dat");
            Serialization.SerializeToDisk<List<JumpBridge>>(EM.JumpBridges, jbFileName);
            MessageBox.Show("Ansiblex gates saved.", "Navigation Structures", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GateSearchFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyJumpBridgeFilter();
        }

        private void ApplyJumpBridgeFilter()
        {
            if (JumpBridgeList.ItemsSource == null)
            {
                return;
            }

            string query = GateSearchFilter.Text?.Trim() ?? string.Empty;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(JumpBridgeList.ItemsSource);

            if (string.IsNullOrWhiteSpace(query))
            {
                view.Filter = null;
                return;
            }

            view.Filter = item =>
            {
                if (item is not EVEData.JumpBridge jb)
                {
                    return false;
                }

                string from = jb.From ?? string.Empty;
                string to = jb.To ?? string.Empty;

                return from.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || to.Contains(query, StringComparison.OrdinalIgnoreCase);
            };
        }

        private void ClearJumpGatesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (EM == null)
            {
                return;
            }

            EM.JumpBridges.Clear();
            EVEData.Navigation.ClearJumpBridges();

            CollectionViewSource.GetDefaultView(JumpBridgeList.ItemsSource).Refresh();
            UpdateJumpBridgeSummary();
        }

        private void DeleteJumpGateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (JumpBridgeList.SelectedIndex == -1 || EM == null)
            {
                return;
            }

            EVEData.JumpBridge jb = JumpBridgeList.SelectedItem as EVEData.JumpBridge;
            EM.JumpBridges.Remove(jb);

            EVEData.Navigation.ClearJumpBridges();
            EVEData.Navigation.UpdateJumpBridges(EM.JumpBridges);

            CollectionViewSource.GetDefaultView(JumpBridgeList.ItemsSource).Refresh();
            UpdateJumpBridgeSummary();
        }

        private void EnableDisableJumpGateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (JumpBridgeList.SelectedIndex == -1 || EM == null)
            {
                return;
            }

            EVEData.JumpBridge jb = JumpBridgeList.SelectedItem as EVEData.JumpBridge;
            jb.Disabled = !jb.Disabled;

            EVEData.Navigation.ClearJumpBridges();
            EVEData.Navigation.UpdateJumpBridges(EM.JumpBridges);

            CollectionViewSource.GetDefaultView(JumpBridgeList.ItemsSource).Refresh();
            UpdateJumpBridgeSummary();
        }

        private void ExportJumpGatesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (EM == null)
            {
                return;
            }

            string exportText = string.Empty;

            foreach (EVEData.MapRegion mr in EM.Regions)
            {
                exportText += "# " + mr.Name + "\n";

                foreach (EVEData.JumpBridge jb in EM.JumpBridges)
                {
                    EVEData.System es = EM.GetEveSystem(jb.From);
                    if (es.Region == mr.Name)
                    {
                        exportText += $"{jb.FromID} {jb.From} --> {jb.To}\n";
                    }

                    es = EM.GetEveSystem(jb.To);
                    if (es.Region == mr.Name)
                    {
                        exportText += $"{jb.ToID} {jb.To} --> {jb.From}\n";
                    }
                }

                exportText += "\n";
            }

            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, exportText);
                }
                catch
                {
                }
            }
        }

        private async void FindJumpGatesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (EM == null)
            {
                return;
            }

            ImportJumpGatesBtn.IsEnabled = false;
            ClearJumpGatesBtn.IsEnabled = false;
            JumpBridgeList.IsEnabled = false;
            ImportPasteJumpGatesBtn.IsEnabled = false;
            ExportJumpGatesBtn.IsEnabled = false;

            string searchText = GateSearchFilter.Text ?? string.Empty;

            foreach (EVEData.LocalCharacter c in EM.LocalCharacters)
            {
                if (c.ESILinked)
                {
                    List<EVEData.JumpBridge> jbl = await c.FindJumpGates(searchText);

                    foreach (EVEData.JumpBridge jb in jbl)
                    {
                        bool found = false;

                        foreach (EVEData.JumpBridge jbr in EM.JumpBridges)
                        {
                            if ((jb.From == jbr.From && jb.To == jbr.To) || (jb.From == jbr.To && jb.To == jbr.From))
                            {
                                found = true;
                            }
                        }

                        if (!found)
                        {
                            EM.JumpBridges.Add(jb);
                        }
                    }
                }
            }

            EVEData.Navigation.ClearJumpBridges();
            EVEData.Navigation.UpdateJumpBridges(EM.JumpBridges);
            UpdateJumpBridgeSummary();

            ImportJumpGatesBtn.IsEnabled = true;
            ClearJumpGatesBtn.IsEnabled = true;
            JumpBridgeList.IsEnabled = true;
            ImportPasteJumpGatesBtn.IsEnabled = true;
            ExportJumpGatesBtn.IsEnabled = true;
            CollectionViewSource.GetDefaultView(JumpBridgeList.ItemsSource).Refresh();
        }

        private void ImportPasteJumpGatesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (EM == null)
            {
                return;
            }

            if (!Clipboard.ContainsText(TextDataFormat.Text))
            {
                return;
            }
            string jbText = Clipboard.GetText(TextDataFormat.UnicodeText);

            Regex rx = new Regex(
                @"<url=showinfo:35841//([0-9]+)>(.*?) » (.*?) - .*?</url>|^[\t ]*([0-9]+) (.*) --> (.*)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled
            );
            MatchCollection matches = rx.Matches(jbText);

            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                long idFrom = 0;
                if (groups[1].Value != "" && groups[2].Value != "" && groups[3].Value != "")
                {
                    long.TryParse(groups[1].Value, out idFrom);
                    string from = groups[2].Value;
                    string to = groups[3].Value;
                    EM.AddUpdateJumpBridge(from, to, idFrom);
                }
                else if (groups[4].Value != "" && groups[5].Value != "" && groups[6].Value != "")
                {
                    long.TryParse(groups[4].Value, out idFrom);
                    string from = groups[5].Value.Trim();
                    string to = groups[6].Value.Trim();
                    EM.AddUpdateJumpBridge(from, to, idFrom);
                }
            }

            Regex arrowRx = new Regex(
                @"^\s*([A-Z0-9\-]+)\s*->\s*([A-Z0-9\-]+)\s*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled
            );

            Regex wikiRx = new Regex(
                @"^\s*.+?\s+([A-Z0-9\-]+)\s+@\s+.*?\s+([A-Z0-9\-]+)\s+@\s+",
                RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled
            );

            string[] lines = jbText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            foreach (string rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                Match arrowMatch = arrowRx.Match(rawLine);
                if (arrowMatch.Success)
                {
                    string from = arrowMatch.Groups[1].Value.Trim();
                    string to = arrowMatch.Groups[2].Value.Trim();
                    EM.AddUpdateJumpBridge(from, to, 0);
                    continue;
                }

                Match wikiMatch = wikiRx.Match(rawLine);
                if (wikiMatch.Success)
                {
                    string from = wikiMatch.Groups[1].Value.Trim();
                    string to = wikiMatch.Groups[2].Value.Trim();
                    EM.AddUpdateJumpBridge(from, to, 0);
                }
            }

            EVEData.Navigation.ClearJumpBridges();
            EVEData.Navigation.UpdateJumpBridges(EM.JumpBridges);
            UpdateJumpBridgeSummary();
            CollectionViewSource.GetDefaultView(JumpBridgeList.ItemsSource).Refresh();
        }

        private void UpdateJumpBridgeSummary()
        {
            if (EM == null)
            {
                return;
            }

            int jbCount = 0;
            int missingInfo = 0;
            int disabled = 0;

            foreach (EVEData.JumpBridge jb in EM.JumpBridges)
            {
                jbCount++;

                if (string.IsNullOrWhiteSpace(jb.From) || string.IsNullOrWhiteSpace(jb.To))
                {
                    missingInfo++;
                }
                else
                {
                    if (EM.GetEveSystem(jb.From) == null || EM.GetEveSystem(jb.To) == null)
                    {
                        missingInfo++;
                    }
                }

                if (jb.Disabled)
                {
                    disabled++;
                }
            }

            string label = $"{jbCount} gates, {missingInfo} Incomplete, {disabled} Disabled";
            AnsiblexSummaryLbl.Text = label;
        }
    }
}
