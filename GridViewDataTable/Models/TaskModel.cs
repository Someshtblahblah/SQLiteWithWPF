
using System.ComponentModel;

namespace GridViewDataTable.Models
{
    public class TaskModel : INotifyPropertyChanged
    {
        private int _taskOrder;
        private string _source;
        private string _target;
        private string _creationTime;
        private string _createdBy;
        private string _metadata;
        private string _settings;

        public event PropertyChangedEventHandler PropertyChanged;

        public int TaskOrder
        {
            get => _taskOrder;
            set
            {
                if (_taskOrder != value)
                {
                    _taskOrder = value;
                    OnPropertyChanged(nameof(TaskOrder));
                }
            }
        }

        public string Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    _source = value;
                    OnPropertyChanged(nameof(Source));
                }
            }
        }

        public string Target
        {
            get => _target;
            set
            {
                if (_target != value)
                {
                    _target = value;
                    OnPropertyChanged(nameof(Target));
                }
            }
        }

        public string CreationTime
        {
            get => _creationTime;
            set
            {
                if (_creationTime != value)
                {
                    _creationTime = value;
                    OnPropertyChanged(nameof(CreationTime));
                }
            }
        }

        public string CreatedBy
        {
            get => _createdBy;
            set
            {
                if (_createdBy != value)
                {
                    _createdBy = value;
                    OnPropertyChanged(nameof(CreatedBy));
                }
            }
        }

        public string Metadata
        {
            get => _metadata;
            set
            {
                if (_metadata != value)
                {
                    _metadata = value;
                    OnPropertyChanged(nameof(Metadata));
                }
            }
        }

        public string Settings
        {
            get => _settings;
            set
            {
                if (_settings != value)
                {
                    _settings = value;
                    OnPropertyChanged(nameof(Settings));
                }
            }
        }

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}