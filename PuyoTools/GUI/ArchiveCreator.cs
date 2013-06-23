﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

using PuyoTools.Modules;
using PuyoTools.Modules.Archive;
using PuyoTools.Modules.Compression;

using Ookii.Dialogs;

namespace PuyoTools.GUI
{
    public partial class ArchiveCreator : ToolForm
    {
        List<ModuleWriterSettings> formatWriterSettings;
        List<Control> writerSettingsControls;
        List<ArchiveFormat> archiveFormats;
        List<CompressionFormat> compressionFormats;

        public ArchiveCreator()
        {
            InitializeComponent();

            // Remove event handlers from the base class
            addFilesButton.Click -= base.addFilesButton_Click;
            addDirectoryButton.Click -= base.addDirectoryButton_Click;

            // Make the list view rows bigger
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(1, 20);
            listView.SmallImageList = imageList;

            // Resize the column widths
            listView_ClientSizeChanged(null, null);

            // Set up the writer settings panel and format writer settings
            formatWriterSettings = new List<ModuleWriterSettings>();
            writerSettingsControls = new List<Control>();

            // Fill the archive format box
            archiveFormatBox.SelectedIndex = 0;
            archiveFormats = new List<ArchiveFormat>();
            foreach (KeyValuePair<ArchiveFormat, ArchiveBase> format in Archive.Formats)
            {
                if (format.Value.CanWrite)
                {
                    archiveFormatBox.Items.Add(format.Value.Name);
                    archiveFormats.Add(format.Key);

                    ModuleWriterSettings writerSettings = format.Value.WriterSettingsObject();
                    if (writerSettings != null)
                    {
                        writerSettingsControls.Add(writerSettings.Content());
                    }
                    else
                    {
                        writerSettingsControls.Add(null);
                    }

                    formatWriterSettings.Add(writerSettings);
                }
            }

            // Fill the compression format box
            compressionFormatBox.SelectedIndex = 0;
            compressionFormats = new List<CompressionFormat>();
            foreach (KeyValuePair<CompressionFormat, CompressionBase> format in Compression.Formats)
            {
                if (format.Value.CanWrite)
                {
                    compressionFormatBox.Items.Add(format.Value.Name);
                    compressionFormats.Add(format.Key);
                }
            }
        }

        private void AddFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                FileEntry fileEntry = new FileEntry();
                fileEntry.SourceFile = file;
                fileEntry.Filename = Path.GetFileName(file);
                fileEntry.FilenameInArchive = fileEntry.Filename;

                ListViewItem item = new ListViewItem(new string[] {
                    (listView.Items.Count + 1).ToString(),
                    fileEntry.Filename,
                    fileEntry.FilenameInArchive,
                });

                item.Tag = fileEntry;

                listView.Items.Add(item);
            }
        }

        private void EnableRunButton()
        {
            runButton.Enabled = (listView.Items.Count > 0 && archiveFormatBox.SelectedIndex > 0);
        }

        private void Run(Settings settings)
        {
            // For some archives, the file needs to be a specific format. As such,
            // they may be rejected when trying to add them. We'll store such files in
            // this list to let the user know they could not be added.
            List<FileEntry> FilesNotAdded = new List<FileEntry>();

            // Create the stream we are going to write the archive to
            Stream destination;
            if (settings.CompressionFormat == CompressionFormat.Unknown)
            {
                // We are not compression the archive. Write directly to the destination
                destination = File.Create(settings.OutFilename);
            }
            else
            {
                // We are compressing the archive. Write to a memory stream first.
                destination = new MemoryStream();
            }

            // Create the archive
            ArchiveWriter archive = Archive.Create(destination, settings.ArchiveFormat, settings.ArchiveSettings);

            // Add the files to the archive. We're going to do this in a try catch since
            // sometimes an exception may be thrown (namely if the archive cannot contain
            // the file the user is trying to add)
            foreach (ListViewItem item in listView.Items)
            {
                FileEntry entry = (FileEntry)item.Tag;

                try
                {
                    archive.AddFile(File.OpenRead(entry.SourceFile), entry.FilenameInArchive, entry.SourceFile);
                }
                catch (CannotAddFileToArchiveException)
                {
                    FilesNotAdded.Add(entry);
                }
            }

            // If filesNotAdded is not empty, then show a message to the user
            // and ask them if they want to continue
            if (FilesNotAdded.Count > 0)
            {
                // ...
            }

            // Finish writing the archive
            archive.Flush();

            // Do we want to compress this archive?
            if (settings.CompressionFormat != CompressionFormat.Unknown)
            {
                destination.Position = 0;

                using (FileStream outStream = File.Create(settings.OutFilename))
                {
                    Compression.Compress(destination, outStream, (int)destination.Length, Path.GetFileName(settings.OutFilename), settings.CompressionFormat);
                }
            }

            destination.Close();

            // The tool is finished doing what it needs to do. We can close it now.
            this.Close();
        }

        private struct FileEntry
        {
            public string SourceFile;
            public string Filename;
            public string FilenameInArchive;
        }

        private void listView_ClientSizeChanged(object sender, EventArgs e)
        {
            int columnWidth = Math.Max(150, (listView.ClientSize.Width - 20 - listView.Columns[0].Width) / 2);
            listView.Columns[1].Width = columnWidth;
            listView.Columns[2].Width = columnWidth;
        }

        private new void addFilesButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All Files (*.*)|*.*";
            ofd.Multiselect = true;
            ofd.Title = "Add Files";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                AddFiles(ofd.FileNames);

                EnableRunButton();
            }
        }

        private new void addDirectoryButton_Click(object sender, EventArgs e)
        {
            VistaFolderBrowserDialog fbd = new VistaFolderBrowserDialog();
            fbd.Description = "Select a directory.";
            fbd.UseDescriptionForTitle = true;

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                if (MessageBox.Show("Include files within subdirectories?", "Subdirectories", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    AddFiles(Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.AllDirectories));
                }
                else
                {
                    AddFiles(Directory.GetFiles(fbd.SelectedPath));
                }

                EnableRunButton();
            }
        }

        private void archiveFormatBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            archiveSettingsPanel.Controls.Clear();

            if (archiveFormatBox.SelectedIndex != 0)
            {
                if (writerSettingsControls[archiveFormatBox.SelectedIndex - 1] != null)
                {
                    archiveSettingsPanel.Controls.Add(writerSettingsControls[archiveFormatBox.SelectedIndex - 1]);
                }
            }

            EnableRunButton();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // The first index in the selected indices.
            // We'll need it for later. I'll explain it then
            int firstIndex = 0;

            // Loop through each selected item (not index)
            foreach (ListViewItem item in listView.SelectedItems)
            {
                // Check to see if this is the first index selected.
                if (item.Index > firstIndex)
                {
                    firstIndex = item.Index;
                }

                listView.Items.Remove(item);
            }

            // Now reorder each item
            for (int i = firstIndex; i < listView.Items.Count; i++)
            {
                listView.Items[i].SubItems[0].Text = (i + 1).ToString();
            }

            EnableRunButton();
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InputDialog dialog = new InputDialog();
            dialog.WindowTitle = "Rename Files";
            dialog.MainInstruction = dialog.WindowTitle;
            dialog.Content = "Selected files will use this filename when added to the archive.";

            if (listView.SelectedItems.Count == 1)
            {
                dialog.Input = listView.SelectedItems[0].SubItems[2].Text;
            }

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foreach (ListViewItem item in listView.SelectedItems)
                {
                    item.SubItems[2].Text = dialog.Input;
                    FileEntry fileEntry = (FileEntry)item.Tag;
                    fileEntry.FilenameInArchive = dialog.Input;
                }
            }
        }

        private void runButton_Click(object sender, EventArgs e)
        {
            // Get the format of the archive the user wants to create
            ArchiveFormat archiveFormat = archiveFormats[archiveFormatBox.SelectedIndex - 1];
            string fileExtension = (Archive.Formats[archiveFormat].FileExtension != String.Empty ? Archive.Formats[archiveFormat].FileExtension : ".*");

            // Prompt the user to save the archive
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save Archive";
            sfd.Filter = Archive.Formats[archiveFormat].Name + " Archive (*" + fileExtension + ")|*" + fileExtension + "|All Files (*.*)|*.*";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                // Disable the form
                this.Enabled = false;

                Settings settings = new Settings();
                settings.ArchiveFormat = archiveFormat;
                settings.OutFilename = sfd.FileName;

                if (compressionFormatBox.SelectedIndex != 0)
                {
                    settings.CompressionFormat = compressionFormats[compressionFormatBox.SelectedIndex - 1];
                }
                else
                {
                    settings.CompressionFormat = CompressionFormat.Unknown;
                }

                settings.ArchiveSettings = formatWriterSettings[archiveFormatBox.SelectedIndex - 1];
                if (settings.ArchiveSettings != null)
                {
                    settings.ArchiveSettings.SetSettings();
                }

                Run(settings);
            }
        }

        private struct Settings
        {
            public ArchiveFormat ArchiveFormat;
            public CompressionFormat CompressionFormat;
            public string OutFilename;
            public ModuleWriterSettings ArchiveSettings;
        }
    }
}
