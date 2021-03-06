﻿//Coded by Goh Chun Lin
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Resources;
using OneSync.Synchronization;

namespace OneSync.UI
{

    // UI Wrapper class for SyncJob and associated SyncAgent
    public class UISyncJobEntry : INotifyPropertyChanged
    {
        private static ResourceManager m_ResourceManager = new ResourceManager(Properties.Settings.Default.LanguageResx,
                                    System.Reflection.Assembly.GetExecutingAssembly());

        public event PropertyChangedEventHandler PropertyChanged;

        private SyncJob _syncJob;
        private FileSyncAgent _agent;
        private bool _isSelected = false, _editMode = false;
        private int _progressbarValue = 0;

        private Visibility _progressbarVisibility = Visibility.Hidden;
        private string _progressbarColor = "#FF01D328"; //This is light green.
        private string _progressbarMessage = "";
        private string _newJobName, _newIntPath, _newSyncSource;

        public UISyncJobEntry(SyncJob job)
        {
            this._syncJob = job;
            this._agent = new FileSyncAgent(job);
        }

        public SyncJob SyncJob
        {
            get { return this._syncJob; }
        }

        public FileSyncAgent SyncAgent
        {
            get { return this._agent; }
        }

        public string SyncSource
        {
            get { return this.SyncJob.SyncSource.Path; }
        }

        public string JobName
        {
            get { return this.SyncJob.Name; }
        }

        public string IntermediaryStoragePath
        {
            get { return this.SyncJob.IntermediaryStorage.Path; }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public bool EditMode
        {
            get { return _editMode; }
            set
            {
                if (_editMode == value) return;

                _editMode = value;

                if (value)
                {
                    this.NewJobName = this.JobName;
                    this.NewIntermediaryStoragePath = this.IntermediaryStoragePath;
                    this.NewSyncSource = this.SyncSource;
                }
                OnPropertyChanged("EditMode");
            }
        }

        public string NewJobName
        {
            get { return _newJobName; }
            set 
            {
                _newJobName = value;
                OnPropertyChanged("NewJobName");
            }
        }

        public string NewIntermediaryStoragePath
        {
            get { return _newIntPath; }
            set
            {
                _newIntPath = value;
                OnPropertyChanged("NewIntermediaryStoragePath");
            }
        }

        public string NewSyncSource
        {
            get { return _newSyncSource; }
            set
            {
                _newSyncSource = value;
                OnPropertyChanged("NewSyncSource");
            }
        }

        public Exception Error { get; set; }

        public OneSync.DropboxStatus DropboxStatus
        {
            get
            {
                try
                {
                    return DropboxStatusCheck.ReturnDropboxStatus(this.SyncJob.IntermediaryStorage.Path);
                }
                catch (Exception) 
                {
                    return DropboxStatus.UP_TO_DATE;
                }
            }
        }

        public int ProgressBarValue
        {
            get { return _progressbarValue; }
            set
            {
                _progressbarValue = value;
                OnPropertyChanged("ProgressBarValue");
            }
        }

        public Visibility ProgressBarVisibility
        {
            get { return _progressbarVisibility; }
            set
            {
                _progressbarVisibility = value;
                OnPropertyChanged("ProgressBarVisibility");
            }
        }

        public string ProgressBarColor 
        {
            get { return _progressbarColor; }
            set
            {
                _progressbarColor = value;
                OnPropertyChanged("ProgressBarColor");
            }
        }

        public string ProgressBarMessage
        {
            get { return _progressbarMessage; }
            set
            {
                _progressbarMessage = value;
                OnPropertyChanged("ProgressBarMessage");
            }
        }

        public void InfoChanged()
        {
            OnPropertyChanged("IntermediaryStoragePath");
            OnPropertyChanged("JobName");
            OnPropertyChanged("SyncSource");
            //OnPropertyChanged("DropboxStatus");
        }

        //Start: Language related
        public string PreviewButtonText
        {
            get { return m_ResourceManager.GetString("btn_preview"); }
        }

        public string ShowLogButtonText
        {
            get { return m_ResourceManager.GetString("btn_showLog"); }
        }

        public string PreviewButtonToolTipText
        {
            get { return m_ResourceManager.GetString("tot_previewJob"); }
        }

        public string ShowLogButtonToolTipText
        {
            get { return m_ResourceManager.GetString("tot_showLog"); }
        }

        public string EditJobButtonToolTipText 
        {
            get { return m_ResourceManager.GetString("tot_editJob"); }
        }

        public string DeleteJobButtonToolTipText
        {
            get { return m_ResourceManager.GetString("tot_deleteJob"); }
        }
        //End: Language related

        public static Queue<UISyncJobEntry> GetSelectedJobs(IEnumerable<UISyncJobEntry> entries)
        {
            Queue<UISyncJobEntry> selectedEntries = new Queue<UISyncJobEntry>();

            foreach (UISyncJobEntry entry in entries)
            {
                if (entry.IsSelected)
                    selectedEntries.Enqueue(entry);
            }

            return selectedEntries;
        }

        protected virtual void OnPropertyChanged(string PropertyName)
        {
            this._agent = new FileSyncAgent(SyncJob);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(PropertyName));
        }
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    [ValueConversion(typeof(bool), typeof(string))]
    public class EditModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return "Resource/save.png";
            else
                return "Resource/edit.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
