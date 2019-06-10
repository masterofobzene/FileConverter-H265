// <copyright file="SettingsViewModel.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ViewModels
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Input;

    using FileConverter.Services;
    using FileConverter.Views;

    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Ioc;
    using GalaSoft.MvvmLight.Messaging;

    using Microsoft.Win32;
    using ImageMagick;

    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private InputExtensionCategory[] inputCategories;
        private PresetFolderNode presetsRootFolder;
        private PresetFolderNode selectedFolder;
        private PresetNode selectedPreset;
        private Settings settings;
        private bool displaySeeChangeLogLink = true;

        private RelayCommand<string> openUrlCommand;
        private RelayCommand getChangeLogContentCommand;
        private RelayCommand createFolderCommand;
        private RelayCommand movePresetUpCommand;
        private RelayCommand movePresetDownCommand;
        private RelayCommand addNewPresetCommand;
        private RelayCommand removePresetCommand;
        private RelayCommand saveCommand;
        private RelayCommand<CancelEventArgs> closeCommand;

        private ListCollectionView outputTypes;
        private CultureInfo[] supportedCultures;

        /// <summary>
        /// Initializes a new instance of the SettingsViewModel class.
        /// </summary>
        public SettingsViewModel()
        {
            if (this.IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
                this.Settings = new Settings();
                this.Settings.ConversionPresets.Add(new ConversionPreset("Test", OutputType.Mp3));
            }
            else
            {
                ISettingsService settingsService = SimpleIoc.Default.GetInstance<ISettingsService>();
                this.Settings = settingsService.Settings;

                List<OutputTypeViewModel> outputTypeViewModels = new List<OutputTypeViewModel>();
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Ogg));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Mp3));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Aac));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Flac));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Wav));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Mkv));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Mp4));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Ogv));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Webm));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Avi));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Png));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Jpg));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Webp));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Ico));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Gif));
                outputTypeViewModels.Add(new OutputTypeViewModel(OutputType.Pdf));
                this.outputTypes = new ListCollectionView(outputTypeViewModels);
                this.outputTypes.GroupDescriptions.Add(new PropertyGroupDescription("Category"));

                this.SupportedCultures = Helpers.GetSupportedCultures().ToArray();

                this.InitializeCompatibleInputExtensions();
                this.InitializePresetFolders();
            }
        }

        public IEnumerable<InputExtensionCategory> InputCategories
        {
            get
            {
                if (this.inputCategories == null)
                {
                    yield break;
                }

                for (int index = 0; index < this.inputCategories.Length; index++)
                {
                    InputExtensionCategory category = this.inputCategories[index];
                    if (this.SelectedPreset == null || Helpers.IsOutputTypeCompatibleWithCategory(this.SelectedPreset.Preset.OutputType, category.Name))
                    {
                        yield return category;
                    }
                }
            }
        }
        
        public InputPostConversionAction[] InputPostConversionActions => new[]
                                                                             {
                                                                                 InputPostConversionAction.None,
                                                                                 InputPostConversionAction.MoveInArchiveFolder,
                                                                                 InputPostConversionAction.Delete,
                                                                             };

        public PresetFolderNode PresetsRootFolder
        {
            get => this.presetsRootFolder;

            set
            {
                this.presetsRootFolder = value;
                this.RaisePropertyChanged();
            }
        }

        public AbstractTreeNode SelectedItem
        {
            get
            {
                if (this.SelectedFolder != null)
                {
                    return this.SelectedFolder;
                }

                return this.SelectedPreset;
            }

            set
            {
                if (value is PresetNode preset)
                {
                    this.SelectedPreset = preset;
                    this.SelectedFolder = null;
                }
                else if (value is PresetFolderNode folder)
                {
                    this.SelectedFolder = folder;
                    this.SelectedPreset = null;
                }
                else
                {
                    this.SelectedPreset = null;
                    this.SelectedFolder = null;
                }

                this.RaisePropertyChanged();
            }
        }

        public PresetFolderNode SelectedFolder
        {
            get => this.selectedFolder;

            set
            {
                this.selectedFolder = value;

                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(this.SelectedItem));
                this.movePresetUpCommand?.RaiseCanExecuteChanged();
                this.movePresetDownCommand?.RaiseCanExecuteChanged();
                this.removePresetCommand?.RaiseCanExecuteChanged();
            }
        }

        public PresetNode SelectedPreset
        {
            get => this.selectedPreset;

            set
            {
                if (this.selectedPreset != null)
                {
                    this.selectedPreset.Preset.PropertyChanged -= this.SelectedPresetPropertyChanged;
                }

                this.selectedPreset = value;

                if (this.selectedPreset != null)
                {
                    this.selectedPreset.Preset.PropertyChanged += this.SelectedPresetPropertyChanged;
                }

                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(this.SelectedItem));
                this.RaisePropertyChanged(nameof(this.InputCategories));
                this.movePresetUpCommand?.RaiseCanExecuteChanged();
                this.movePresetDownCommand?.RaiseCanExecuteChanged();
                this.removePresetCommand?.RaiseCanExecuteChanged();
            }
        }

        public Settings Settings
        {
            get => this.settings;

            set
            {
                this.settings = value;
                this.RaisePropertyChanged();
            }
        }

        public CultureInfo[] SupportedCultures
        {
            get => this.supportedCultures;
            set
            {
                this.supportedCultures = value;
                this.RaisePropertyChanged();
            }
        }

        public ListCollectionView OutputTypes
        {
            get => this.outputTypes;
            set
            {
                this.outputTypes = value;
                this.RaisePropertyChanged();
            }
        }
        
        public bool DisplaySeeChangeLogLink
        {
            get
            {
                return this.displaySeeChangeLogLink;
            }

            private set
            {
                this.displaySeeChangeLogLink = value;

                this.RaisePropertyChanged();
            }
        }
        
        public ICommand GetChangeLogContentCommand
        {
            get
            {
                if (this.getChangeLogContentCommand == null)
                {
                    this.getChangeLogContentCommand = new RelayCommand(this.DownloadChangeLogAction);
                }

                return this.getChangeLogContentCommand;
            }
        }

        public ICommand OpenUrlCommand
        {
            get
            {
                if (this.openUrlCommand == null)
                {
                    this.openUrlCommand = new RelayCommand<string>((url) => Process.Start(url));
                }

                return this.openUrlCommand;
            }
        }

        public ICommand CreateFolderCommand
        {
            get
            {
                if (this.createFolderCommand == null)
                {
                    this.createFolderCommand = new RelayCommand(this.CreateFolder);
                }

                return this.createFolderCommand;
            }
        }

        public ICommand MovePresetUpCommand
        {
            get
            {
                if (this.movePresetUpCommand == null)
                {
                    this.movePresetUpCommand = new RelayCommand(this.MoveSelectedPresetUp, this.CanMoveSelectedPresetUp);
                }

                return this.movePresetUpCommand;
            }
        }

        public ICommand MovePresetDownCommand
        {
            get
            {
                if (this.movePresetDownCommand == null)
                {
                    this.movePresetDownCommand = new RelayCommand(this.MoveSelectedPresetDown, this.CanMoveSelectedPresetDown);
                }

                return this.movePresetDownCommand;
            }
        }

        public ICommand AddNewPresetCommand
        {
            get
            {
                if (this.addNewPresetCommand == null)
                {
                    this.addNewPresetCommand = new RelayCommand(this.AddNewPreset);
                }

                return this.addNewPresetCommand;
            }
        }

        public ICommand RemoveSelectedPresetCommand
        {
            get
            {
                if (this.removePresetCommand == null)
                {
                    this.removePresetCommand = new RelayCommand(this.RemoveSelectedPreset, this.CanRemoveSelectedPreset);
                }
                
                return this.removePresetCommand;
            }
        }

        public ICommand SaveCommand
        {
            get
            {
                if (this.saveCommand == null)
                {
                    this.saveCommand = new RelayCommand(this.SaveSettings, this.CanSaveSettings);
                }

                return this.saveCommand;
            }
        }

        public ICommand CloseCommand
        {
            get
            {
                if (this.closeCommand == null)
                {
                    this.closeCommand = new RelayCommand<CancelEventArgs>(this.CloseSettings);
                }

                return this.closeCommand;
            }
        }

        public TreeViewSelectionBehavior.IsChildOfPredicate PresetsHierarchyPredicate => (object nodeA, object nodeB) =>
            {
                if (nodeA is PresetNode)
                {
                    return false;
                }

                PresetFolderNode parentFolder = nodeA as PresetFolderNode;
                Diagnostics.Debug.Assert(parentFolder != null, "Node should be a preset folder.");

                return parentFolder.IsNodeInHierarchy(nodeB as AbstractTreeNode, true);
            };

        private void SelectedPresetPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == "OutputType")
            {
                this.RaisePropertyChanged(nameof(this.InputCategories));
            }

            this.saveCommand.RaiseCanExecuteChanged();
        }

        private void DownloadChangeLogAction()
        {
            IUpgradeService upgradeService = SimpleIoc.Default.GetInstance<IUpgradeService>();
            upgradeService.DownloadChangeLog();
            this.DisplaySeeChangeLogLink = false;
        }

        private void InitializeCompatibleInputExtensions()
        {
            RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\FileConverter");
            if (registryKey == null)
            {
                MessageBox.Show("Can't retrieve the list of compatible input extensions. (code 0x09)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string registryValue = registryKey.GetValue("CompatibleInputExtensions") as string;
            if (registryValue == null)
            {
                MessageBox.Show("Can't retrieve the list of compatible input extensions. (code 0x0A)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] compatibleInputExtensions = registryValue.Split(';');

            List<InputExtensionCategory> categories = new List<InputExtensionCategory>();
            for (int index = 0; index < compatibleInputExtensions.Length; index++)
            {
                string compatibleInputExtension = compatibleInputExtensions[index];
                string extensionCategory = Helpers.GetExtensionCategory(compatibleInputExtension);
                InputExtensionCategory category = categories.Find(match => match.Name == extensionCategory);
                if (category == null)
                {
                    category = new InputExtensionCategory(extensionCategory);
                    categories.Add(category);
                }

                category.AddExtension(compatibleInputExtension);
            }

            this.inputCategories = categories.ToArray();
            this.RaisePropertyChanged(nameof(this.InputCategories));
        }

        private void InitializePresetFolders()
        {
            this.presetsRootFolder = new PresetFolderNode(null, null);
            foreach (ConversionPreset preset in this.Settings.ConversionPresets)
            {
                PresetFolderNode parent = this.presetsRootFolder;
                foreach (string folderName in preset.ParentFoldersNames)
                {
                    PresetFolderNode subFolder = parent.Children.FirstOrDefault(match => match is PresetFolderNode && ((PresetFolderNode)match).Name == folderName) as PresetFolderNode;
                    if (subFolder == null)
                    {
                        subFolder = new PresetFolderNode(folderName, parent);
                        parent.Children.Add(subFolder);
                    }

                    parent = subFolder;
                }

                PresetNode presetNode = new PresetNode(preset, parent);
                parent.Children.Add(presetNode);
            }

            this.RaisePropertyChanged(nameof(this.PresetsRootFolder));
        }

        private void ComputePresetsParentFoldersNames(AbstractTreeNode node, List<string> folderNamesCache)
        {
            if (node is PresetFolderNode folder)
            {
                if (!string.IsNullOrEmpty(folder.Name))
                {
                    folderNamesCache.Add(folder.Name);
                }

                foreach (var child in folder.Children)
                {
                    this.ComputePresetsParentFoldersNames(child, folderNamesCache);
                }

                if (!string.IsNullOrEmpty(folder.Name))
                {
                    folderNamesCache.RemoveAt(folderNamesCache.Count - 1);
                }
            }
            else if (node is PresetNode preset)
            {
                preset.Preset.ParentFoldersNames = folderNamesCache.ToArray();
            }
        }

        private void CloseSettings(CancelEventArgs args)
        {
            ISettingsService settingsService = SimpleIoc.Default.GetInstance<ISettingsService>();
            settingsService.RevertSettings();

            INavigationService navigationService = SimpleIoc.Default.GetInstance<INavigationService>();
            navigationService.Close(Pages.Settings, args != null);
        }

        private bool CanSaveSettings()
        {
            return this.settings != null && string.IsNullOrEmpty(this.settings.Error);
        }

        private void SaveSettings()
        {
            // Compute parent folder names.
            this.ComputePresetsParentFoldersNames(this.presetsRootFolder, new List<string>());

            // Save changes.
            ISettingsService settingsService = SimpleIoc.Default.GetInstance<ISettingsService>();
            settingsService.SaveSettings();

            INavigationService navigationService = SimpleIoc.Default.GetInstance<INavigationService>();
            navigationService.Close(Pages.Settings, false);
        }

        private void CreateFolder()
        {
            PresetFolderNode parent;
            if (this.SelectedFolder != null)
            {
                parent = this.SelectedFolder;
            }
            else if (this.SelectedItem != null)
            {
                parent = this.SelectedItem.Parent;
            }
            else
            {
                parent = this.presetsRootFolder;
            }

            int insertIndex = parent.Children.IndexOf(this.SelectedItem) + 1;
            if (insertIndex < 0)
            {
                insertIndex = parent.Children.Count;
            }

            // Generate a unique folder name.
            string folderName = Properties.Resources.DefaultFolderName;
            int index = 1;
            while (parent.Children.Any(match => match is PresetFolderNode folder && folder.Name == folderName))
            {
                index++;
                folderName = $"{Properties.Resources.DefaultFolderName} ({index})";
            }

            PresetFolderNode newFolder = new PresetFolderNode(folderName, parent);

            parent.Children.Insert(insertIndex, newFolder);

            this.SelectedItem = newFolder;

            Messenger.Default.Send<string>("FolderName", "DoFocus");
        }

        private bool CanMoveSelectedPresetUp()
        {
            if (this.SelectedItem == null)
            {
                // no preset selected.
                return false;
            }

            if (this.SelectedItem.Parent != this.presetsRootFolder)
            {
                // The parent is not the root, we can move the preset up.
                return true;
            }

            int indexOfSelectedPreset = this.SelectedItem.Parent.Children.IndexOf(this.SelectedItem);
            return indexOfSelectedPreset > 0;
        }

        private void MoveSelectedPresetUp()
        {
            Diagnostics.Debug.Assert(this.SelectedItem != null, "this.SelectedItem != null");

            AbstractTreeNode itemToMoveUp = this.SelectedItem;
            PresetFolderNode currentParent = itemToMoveUp.Parent;
            int indexOfSelectedPreset = currentParent.Children.IndexOf(itemToMoveUp);
            if (indexOfSelectedPreset == 0)
            {
                // Move to the parent folder.
                int indexOfFolder = currentParent.Parent.Children.IndexOf(currentParent);
                currentParent.Children.RemoveAt(indexOfSelectedPreset);
                currentParent.Parent.Children.Insert(indexOfFolder, itemToMoveUp);
                itemToMoveUp.Parent = currentParent.Parent;
            }
            else
            {
                int newIndexOfSelectedPreset = System.Math.Max(0, indexOfSelectedPreset - 1);
                if (currentParent.Children[newIndexOfSelectedPreset] is PresetFolderNode newParent)
                {
                    // Move to child folder.
                    currentParent.Children.RemoveAt(indexOfSelectedPreset);
                    newParent.Children.Add(itemToMoveUp);
                    itemToMoveUp.Parent = newParent;
                }
                else
                {
                    // Swap nodes.
                    currentParent.Children.Move(indexOfSelectedPreset, newIndexOfSelectedPreset);
                }
            }

            this.SelectedItem = itemToMoveUp;
            this.movePresetUpCommand?.RaiseCanExecuteChanged();
            this.movePresetDownCommand?.RaiseCanExecuteChanged();
        }

        private bool CanMoveSelectedPresetDown()
        {
            if (this.SelectedItem == null)
            {
                // no preset selected.
                return false;
            }

            if (this.SelectedItem.Parent != this.presetsRootFolder)
            {
                // The parent is not the root, we can move the preset down.
                return true;
            }

            int indexOfSelectedPreset = this.SelectedItem.Parent.Children.IndexOf(this.SelectedItem);
            return indexOfSelectedPreset < this.SelectedItem.Parent.Children.Count - 1;
        }

        private void MoveSelectedPresetDown()
        {
            Diagnostics.Debug.Assert(this.SelectedItem != null, "this.SelectedItem != null");

            AbstractTreeNode itemToMoveDown = this.SelectedItem;
            PresetFolderNode currentParent = itemToMoveDown.Parent;
            int indexOfSelectedPreset = currentParent.Children.IndexOf(itemToMoveDown);
            if (indexOfSelectedPreset == currentParent.Children.Count - 1)
            {
                // Move to the parent folder.
                int indexOfFolder = currentParent.Parent.Children.IndexOf(currentParent);
                currentParent.Children.RemoveAt(indexOfSelectedPreset);
                currentParent.Parent.Children.Insert(indexOfFolder  + 1, itemToMoveDown);
                itemToMoveDown.Parent = currentParent.Parent;
            }
            else
            {
                int newIndexOfSelectedPreset = System.Math.Min(this.SelectedItem.Parent.Children.Count - 1, indexOfSelectedPreset + 1);
                if (currentParent.Children[newIndexOfSelectedPreset] is PresetFolderNode newParent)
                {
                    // Move to child folder.
                    currentParent.Children.RemoveAt(indexOfSelectedPreset);
                    newParent.Children.Insert(0, itemToMoveDown);
                    itemToMoveDown.Parent = newParent;
                }
                else
                {
                    // Swap nodes.
                    currentParent.Children.Move(indexOfSelectedPreset, newIndexOfSelectedPreset);
                }
            }

            this.SelectedItem = itemToMoveDown;

            this.movePresetUpCommand?.RaiseCanExecuteChanged();
            this.movePresetDownCommand?.RaiseCanExecuteChanged();
        }

        private void AddNewPreset()
        {
            PresetFolderNode parent;
            if (this.SelectedFolder != null)
            {
                parent = this.SelectedFolder;
            }
            else if (this.SelectedItem != null)
            {
                parent = this.SelectedItem.Parent;
            }
            else
            {
                parent = this.presetsRootFolder;
            }

            int insertIndex = parent.Children.IndexOf(this.SelectedItem) + 1;
            if (insertIndex < 0)
            {
                insertIndex = parent.Children.Count;
            }

            // Generate a unique preset name.
            string presetName = Properties.Resources.DefaultPresetName;
            int index = 1;
            while (parent.Children.Any(match => match is PresetNode folder && folder.Preset.ShortName == presetName))
            {
                index++;
                presetName = $"{Properties.Resources.DefaultPresetName} ({index})";
            }

            // Create preset by copying the selected one.
            ConversionPreset newPreset = null;
            if (this.SelectedPreset != null)
            {
                newPreset = new ConversionPreset(presetName, this.SelectedPreset.Preset);
            }
            else
            {
                newPreset = new ConversionPreset(presetName, OutputType.Mkv, new string[0]);
            }

            PresetNode node = new PresetNode(newPreset, parent);

            parent.Children.Insert(insertIndex, node);

            this.SelectedItem = node;

            Messenger.Default.Send<string>("PresetName", "DoFocus");

            this.removePresetCommand.RaiseCanExecuteChanged();
        }

        private void RemoveSelectedPreset()
        {
            this.SelectedItem.Parent.Children.Remove(this.SelectedItem);

            this.SelectedItem = null;

            this.removePresetCommand.RaiseCanExecuteChanged();
        }

        private bool CanRemoveSelectedPreset()
        {
            return this.SelectedItem != null;
        }
    }
}
