﻿using BladestormSE.Resources;
using Isolib.IOPackage;
using Isolib.STFSPackage;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xceed.Wpf.Toolkit;
using MessageBox = System.Windows.MessageBox;

namespace BladestormSE
{
    public partial class MainWindow
    {
        #region Variables

        private List<Slot> Slots;
        private string _filepath;
        private long _offset;
        private Stfs _stfs;
        private byte[] buffer;
        private int index;

        #endregion Variables

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Input

        private void OpenFile()
        {
            try
            {
                var open = new OpenFileDialog
                           {
                               Title = "Open Xbox360 Bladestorm Save",
                               Filter = "BLADESTORM_*|*BLADESTORM_EU;*BLADESTORM_NA",
                               CheckFileExists = true,
                               Multiselect = false
                           };
                SetStatus("Opening Save");
                //If user cancels or otherwise does not select a save, return.
                if ((bool)!open.ShowDialog())
                {
                    //abort load
                    SetStatus("Idle");
                    return;
                }
                _filepath = open.FileName;
                SetStatus(("Reading " + open.SafeFileName));

                _stfs = new Stfs(_filepath);
                buffer = _stfs.Extract(0);
                ReadID();
                ScanSlots();
                SetStatus("Loaded!");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void ScanSlots()
        {
            int[] offsets =
            {
                0xE35800,
                0xE35830,
                0xE35850
            };
            Slots = new List<Slot>();
            using (var reader = new Reader(buffer, true))
            {
                var constructor = new StringBuilder();
                for (int i = 0; i < 30; i++)
                {
                    SetStatus("Scanning Slot#" + (i + 1).ToString(CultureInfo.InvariantCulture));

                    //Pre-scan for name entries to map 'empty' slots. This accomodates foreign languages.
                    reader.Position = 0xE35850 + (128 * i);
                    if (reader.ReadInt8() == 0) continue;

                    constructor.Append("Slot#" + (i + 1));
                    //Get Locale, Time & Name
                    for (int o = 0; o < 3; o++)
                    {
                        constructor.Append(", ");
                        reader.Position = offsets[o] + (128 * i);
                        constructor.Append(reader.ReadString(StringType.Ascii, 16).Replace("\0", ""));
                        // .Trim(Convert.ToChar("\0")));
                    }

                    ////Build List

                    //SaveSlot.Items.Add(constructor.ToString());
                    Slots.Add(new Slot
                              {
                                  SlotString = constructor.ToString(),
                                  StartingOffset = 0x44 + (0x79400 * (i + 1)),
                                  SlotID = i
                              });
                    constructor.Clear();
                }
                //TODO Add flag for failed load, reset editor state.
                if (Slots.Count == 0)
                {
                    //If no slots are used, editing is pointless since there's nothing to work with.
                    MessageBox.Show(
                        "This save appears to be empty!\nIf this save does contain a used slot, an error has occurred.\nEither way, aborting load operation.",
                        "Error!", MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    return;
                }
                //Load first valid slot
                SaveSlot.Items.Clear();
                foreach (Slot slot in Slots)
                {
                    var item = new ComboBoxItem { Tag = slot.SlotID, Content = slot.SlotString };
                    SaveSlot.Items.Add(item);
                }
                SaveSlot.Items.Refresh();
                SaveSlot.SelectedIndex = 0;
                SaveSlot.SelectionChanged += SetIndex;
                LoadSlot();
            }
        }

        private void SetIndex(object o, SelectionChangedEventArgs e)
        {
            index = (int)((ComboBoxItem)SaveSlot.SelectedItem).Tag;
            LoadSlot();
        }

        private void ReadSlot()
        {
            using (var reader = new Reader(buffer, true))
            {
                SetStatus("Reading Slot# " + (index + 1));
                _offset = (0x44 + (0x79400 * index));
                try
                {
                    //Name
                    SetStatus("Reading Name");
                    reader.Position = _offset + 4;
                    Slots[index].Name =
                        reader.ReadString(StringType.Ascii, 16).Trim(Convert.ToChar("\0"));

                    //Money
                    SetStatus("Reading Money");
                    reader.Position = _offset + 64;
                    Slots[index].Money = reader.ReadUInt32();

                    //Squad Reads
                    int counter = 0;
                    foreach (Squad squad in Slots[index].Squads)
                    {
                        SetStatus("Reading " + (Squaddies)counter);
                        reader.Position = _offset + squad.Adjust;
                        squad.Level = reader.ReadUInt16();
                        reader.Position += 4;
                        squad.Points = reader.ReadUInt32();
                        counter++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    SetStatus("ERROR!");
                    return;
                }
                Slots[index].SlotRead = true;
                SetStatus("Loaded!");
                reader.Flush();
            }
        }

        private void LoadSlot()
        {
            if (!Slots[index].SlotRead)
            {
                //If the slot hasn't been loaded into memory, do it.
                ReadSlot();
            }
            Slot s = Slots[index];

            //Character
            var nameBind = new Binding("Name")
                           {
                               Source = s,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            CharName.SetBinding(TextBox.TextProperty, nameBind);

            //Money
            var moneyBind = new Binding("Money")
                            {
                                Source = s,
                                Mode = BindingMode.TwoWay,
                                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                            };
            MoneyBox.SetBinding(IntegerUpDown.ValueProperty, moneyBind);

            //Knives
            var kniveLv = new Binding("Level")
                          {
                              Source = s.Knives,
                              Mode = BindingMode.TwoWay,
                              UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                          };
            knives.Levelbox.SetBinding(ShortUpDown.ValueProperty, kniveLv);
            var knivePoints = new Binding("Points")
                              {
                                  Source = s.Knives,
                                  Mode = BindingMode.TwoWay,
                                  UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                              };
            knives.PointBox.SetBinding(LongUpDown.ValueProperty, knivePoints);

            //Rapier
            var rapierLv = new Binding("Level")
                           {
                               Source = s.Rapier,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            rapiers.Levelbox.SetBinding(ShortUpDown.ValueProperty, rapierLv);
            var rapierPoints = new Binding("Points")
                               {
                                   Source = s.Rapier,
                                   Mode = BindingMode.TwoWay,
                                   UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                               };
            rapiers.PointBox.SetBinding(LongUpDown.ValueProperty, rapierPoints);

            //Swords
            var swordsLv = new Binding("Level")
                           {
                               Source = s.Swords,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            swords.Levelbox.SetBinding(ShortUpDown.ValueProperty, swordsLv);
            var swordsPoints = new Binding("Points")
                               {
                                   Source = s.Swords,
                                   Mode = BindingMode.TwoWay,
                                   UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                               };
            swords.PointBox.SetBinding(LongUpDown.ValueProperty, swordsPoints);

            //Spears
            var spearLv = new Binding("Level")
                          {
                              Source = s.Spears,
                              Mode = BindingMode.TwoWay,
                              UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                          };
            spears.Levelbox.SetBinding(ShortUpDown.ValueProperty, spearLv);
            var spearPoints = new Binding("Points")
                              {
                                  Source = s.Spears,
                                  Mode = BindingMode.TwoWay,
                                  UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                              };
            spears.PointBox.SetBinding(LongUpDown.ValueProperty, spearPoints);

            //Long Spears
            var longspearLv = new Binding("Level")
                              {
                                  Source = s.LongSpears,
                                  Mode = BindingMode.TwoWay,
                                  UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                              };
            longspears.Levelbox.SetBinding(ShortUpDown.ValueProperty, longspearLv);
            var longspearPoints = new Binding("Points")
                                  {
                                      Source = s.LongSpears,
                                      Mode = BindingMode.TwoWay,
                                      UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                                  };
            longspears.PointBox.SetBinding(LongUpDown.ValueProperty, longspearPoints);

            //Horses
            var hoLv = new Binding("Level")
                       {
                           Source = s.Horses,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            horses.Levelbox.SetBinding(ShortUpDown.ValueProperty, hoLv);
            var hoPoints = new Binding("Points")
                           {
                               Source = s.Horses,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            horses.PointBox.SetBinding(LongUpDown.ValueProperty, hoPoints);

            //Halberds
            var haLv = new Binding("Level")
                       {
                           Source = s.Halberds,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            halberds.Levelbox.SetBinding(ShortUpDown.ValueProperty, haLv);
            var haPoints = new Binding("Points")
                           {
                               Source = s.Halberds,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            halberds.PointBox.SetBinding(LongUpDown.ValueProperty, haPoints);

            //Axes
            var axLv = new Binding("Level")
                       {
                           Source = s.Axes,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            axes.Levelbox.SetBinding(ShortUpDown.ValueProperty, axLv);
            var axPoints = new Binding("Points")
                           {
                               Source = s.Axes,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            axes.PointBox.SetBinding(LongUpDown.ValueProperty, axPoints);

            //Clubs
            var clLv = new Binding("Level")
                       {
                           Source = s.Clubs,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            clubs.Levelbox.SetBinding(ShortUpDown.ValueProperty, clLv);
            var clPoints = new Binding("Points")
                           {
                               Source = s.Clubs,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            clubs.PointBox.SetBinding(LongUpDown.ValueProperty, clPoints);

            //Bows
            var boLv = new Binding("Level")
                       {
                           Source = s.Bows,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            bows.Levelbox.SetBinding(ShortUpDown.ValueProperty, boLv);
            var boPoints = new Binding("Points")
                           {
                               Source = s.Bows,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            bows.PointBox.SetBinding(LongUpDown.ValueProperty, boPoints);

            //HorseBows
            var hbLv = new Binding("Level")
                       {
                           Source = s.HorseBows,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            horsebows.Levelbox.SetBinding(ShortUpDown.ValueProperty, hbLv);
            var hbPoints = new Binding("Points")
                           {
                               Source = s.HorseBows,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            horsebows.PointBox.SetBinding(LongUpDown.ValueProperty, hbPoints);

            //Camels
            var caLv = new Binding("Level")
                       {
                           Source = s.Camels,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            camels.Levelbox.SetBinding(ShortUpDown.ValueProperty, caLv);
            var caPoints = new Binding("Points")
                           {
                               Source = s.Camels,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            camels.PointBox.SetBinding(LongUpDown.ValueProperty, caPoints);

            //Elephants
            var elLv = new Binding("Level")
                       {
                           Source = s.Elephants,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            elephants.Levelbox.SetBinding(ShortUpDown.ValueProperty, elLv);
            var elPoints = new Binding("Points")
                           {
                               Source = s.Elephants,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            elephants.PointBox.SetBinding(LongUpDown.ValueProperty, elPoints);

            //Chariots
            var charLv = new Binding("Level")
                         {
                             Source = s.Chariots,
                             Mode = BindingMode.TwoWay,
                             UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                         };
            chariots.Levelbox.SetBinding(ShortUpDown.ValueProperty, charLv);
            var charPoints = new Binding("Points")
                             {
                                 Source = s.Chariots,
                                 Mode = BindingMode.TwoWay,
                                 UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                             };
            chariots.PointBox.SetBinding(LongUpDown.ValueProperty, charPoints);

            //Explosives
            var exLv = new Binding("Level")
                       {
                           Source = s.Explosives,
                           Mode = BindingMode.TwoWay,
                           UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                       };
            explosives.Levelbox.SetBinding(ShortUpDown.ValueProperty, exLv);
            var exPoints = new Binding("Points")
                           {
                               Source = s.Explosives,
                               Mode = BindingMode.TwoWay,
                               UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                           };
            explosives.PointBox.SetBinding(LongUpDown.ValueProperty, exPoints);

            //Magic
            var magicLv = new Binding("Level")
                          {
                              Source = s.Magic,
                              Mode = BindingMode.TwoWay,
                              UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                          };
            magic.Levelbox.SetBinding(ShortUpDown.ValueProperty, magicLv);
            var magicPoints = new Binding("Points")
                              {
                                  Source = s.Magic,
                                  Mode = BindingMode.TwoWay,
                                  UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                              };
            magic.PointBox.SetBinding(LongUpDown.ValueProperty, magicPoints);

            //Engineers
            var engineerLv = new Binding("Level")
                             {
                                 Source = s.Engineers,
                                 Mode = BindingMode.TwoWay,
                                 UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                             };
            engineers.Levelbox.SetBinding(ShortUpDown.ValueProperty, engineerLv);
            var engineerPoints = new Binding("Points")
                                 {
                                     Source = s.Engineers,
                                     Mode = BindingMode.TwoWay,
                                     UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                                 };
            engineers.PointBox.SetBinding(LongUpDown.ValueProperty, engineerPoints);
        }

        public void ReadID()
        {
            //Profile ID
            PID.Dispatcher.Invoke(new Action(() => PID.Text = _stfs.HeaderData.ProfileID));

            //Console ID
            CID.Dispatcher.Invoke(new Action(() => CID.Text = _stfs.HeaderData.ConsoleID));

            //Title ID - Smallbug if not substringed
            GID.Dispatcher.Invoke(new Action(() => GID.Text = _stfs.HeaderData.TitleID.Substring(8)));

            //Device ID
            DID.Dispatcher.Invoke(new Action(() => DID.Text = _stfs.HeaderData.DeviceID));

            //Save Name
            ContentBox.Dispatcher.Invoke(new Action(() => ContentBox.Text = _stfs.HeaderData.DisplayName));
        }

        #endregion Input

        #region Output

        private void Save(object sender, RoutedEventArgs e)
        {
            using (var writer = new Writer(buffer))
            {
                foreach (Slot slot in Slots.Where(slot => slot.SlotRead))
                {
                    int id = slot.SlotID;

                    SetStatus("Writing Slot# " + (id + 1));
                    _offset = (0x44 + (0x79400 * id));
                    try
                    {
                        //Name
                        SetStatus("Writing Name");
                        writer.Position = _offset + 4;
                        writer.WriteString(slot.Name, StringType.Ascii, 16);

                        //Money
                        SetStatus("Writing Money");
                        writer.Position = _offset + 64;
                        writer.WriteUInt32(slot.Money);

                        ////Inventory
                        //Weapons
                        if (weapons.IsChecked == true)
                        {
                            writer.Position = _offset + 107;
                            writer.WriteHexString(
                                "636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                                ;
                        }
                        //Headgear
                        if (headgear.IsChecked == true)
                        {
                            writer.Position = _offset + 362;
                            writer.WriteHexString(
                                "63636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                                ;
                        }

                        if (bodywear.IsChecked == true)
                        {
                            writer.Position = _offset + 411;
                            writer.WriteHexString(
                                "6363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                                ;
                        }
                        if (armwear.IsChecked == true)
                        {
                            writer.Position = _offset + 473;
                            writer.WriteHexString(
                                "6363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                                ;
                        }
                        if (legwear.IsChecked == true)
                        {
                            writer.Position = _offset + 535;
                            writer.WriteHexString(
                                "6363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                                ;
                        }
                        if (pennon.IsChecked == true)
                        {
                            writer.Position = _offset + 597;
                            writer.WriteHexString(
                                "6363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                                ;
                        }

                        //Squad Writes
                        int counter = 0;
                        foreach (Squad squad in slot.Squads)
                        {
                            SetStatus("Writing " + (Squaddies)counter);
                            writer.Position = _offset + squad.Adjust;
                            writer.WriteUInt16(squad.Level);
                            writer.Position += 4;
                            writer.WriteUInt32(squad.Points);
                            counter++;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        SetStatus("ERROR!");
                        return;
                    }
                    SetStatus("Saved!");
                    writer.Flush();
                }
            }
            _stfs.Replace(buffer, 0);
            //Device ID
            _stfs.HeaderData.DeviceID = DID.Text;

            //Profile ID
            _stfs.HeaderData.ProfileID = PID.Text;

            //Console ID
            _stfs.HeaderData.ConsoleID = CID.Text;
            _stfs.Finish();
            MessageBox.Show("Save Complete.\nPlease close the editor before moving the save back.");
        }

        #endregion Output

        #region Utilities

        private void MaxLevelBtnClick(object sender, RoutedEventArgs e)
        {
            {
                foreach (SquadControl squad in (Squads1.Children.OfType<SquadControl>()))
                {
                    squad.MaxLevel(null, null);
                }
                foreach (SquadControl squad in (Squads2.Children.OfType<SquadControl>()))
                {
                    squad.MaxLevel(null, null);
                }
                foreach (SquadControl squad in (Squads3.Children.OfType<SquadControl>()))
                {
                    squad.MaxLevel(null, null);
                }
            }
        }

        private void MaxPointsBtnClick(object sender, RoutedEventArgs e)
        {
            {
                foreach (SquadControl squad in (Squads1.Children.OfType<SquadControl>()))
                {
                    squad.MaxPoints(null, null);
                }
                foreach (SquadControl squad in (Squads2.Children.OfType<SquadControl>()))
                {
                    squad.MaxPoints(null, null);
                }
                foreach (SquadControl squad in (Squads3.Children.OfType<SquadControl>()))
                {
                    squad.MaxPoints(null, null);
                }
            }
        }

        private void MaxMoneyClick(object sender, RoutedEventArgs e)
        {
            MoneyBox.Value = 0x00FFFFFF;
        }

        private void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AllcheckClick(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox check in checkwrap.Children.OfType<CheckBox>())
            {
                //A toggle switch, why this isnt part of the framework who knows.
                check.IsChecked = check.IsChecked != true;
            }
        }

        private void SetStatus(string text)
        {
            if (status.Dispatcher.CheckAccess())
                status.Content = ("Status: " + text);
            else
            {
                status.Dispatcher.Invoke(new Action(delegate { status.Content = ("Status: " + text); }));
            }
        }

        //protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        //{
        //    base.OnMouseLeftButtonDown(e);

        //    // Begin dragging the window
        //    DragMove();
        //}

        private void OpenButtonClick(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        #region ID Management

        private void NullidbtnClick(object sender, RoutedEventArgs e)
        {
            PID.Text = "E000000000000000";
            DID.Text = "0000000000000000000000000000000000000000";
            CID.Text = "0000000000";
        }

        private void RestoreidbtnClick(object sender, RoutedEventArgs e)
        {
            ReadID();
        }

        private void TransferidbtnClick(object sender, RoutedEventArgs e)
        {
            //TODO Implement this
        }

        #endregion ID Management

        #endregion Utilities
    }
}