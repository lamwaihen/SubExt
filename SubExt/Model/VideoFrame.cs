using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml;
using Windows.Foundation;
using Windows.Storage;

namespace SubExt.Model
{
    public class VideoFrame : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int ID
        {
            get { return _id; }
            set { _id = value; RaisePropertyChanged(); }
        }
        private int _id;
        public TimeSpan BeginTime
        {
            get { return _beginTime; }
            set { _beginTime = value; RaisePropertyChanged(); }
        }
        private TimeSpan _beginTime;
        public TimeSpan EndTime
        {
            get { return _endTime; }
            set { _endTime = value; RaisePropertyChanged(); }
        }
        private TimeSpan _endTime;
        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value; RaisePropertyChanged(); }
        }
        private string _subtitle;
        public bool UserEdited
        {
            get { return _userEdited; }
            set { _userEdited = value; RaisePropertyChanged(); }
        }
        private bool _userEdited = false;
        public StorageFile ImageFile
        {
            get { return _imageFile; }
            set { _imageFile = value; RaisePropertyChanged(); }
        }
        private StorageFile _imageFile;
        public Size ImageSize
        {
            get { return _imageSize; }
            set { _imageSize = value; RaisePropertyChanged(); }
        }
        private Size _imageSize;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Create a copy of a SubtitleExtractor to save.
        // If your object is databound, this copy is not databound.
        public VideoFrame GetCopy()
        {
            VideoFrame copy = (VideoFrame)this.MemberwiseClone();
            return copy;
        }

        public static string SerializeToXML(VideoFrame frame)
        {
            string result = "<VideoFrame ";
            result += "ID=\"" + frame.ID + "\" ";
            result += "BeginTime=\"" + frame.BeginTime.ToString(@"hh\:mm\:ss\,fff") + "\" ";
            result += "EndTime=\"" + frame.EndTime.ToString(@"hh\:mm\:ss\,fff") + "\" ";
            result += "UserEdited=\"" + frame.UserEdited.ToString() + "\">\n";
            result += "<Subtitle>" + frame.Subtitle + "</Subtitle>\n";
            result += "<File Width=\"" + frame.ImageSize.Width + "\" Height=\"" + frame.ImageSize.Height + "\">" + frame.ImageFile.Name + "</File>\n";
            result += "</VideoFrame>\n";
            return result;
        }

        public static void UpdateXMLReader(VideoFrame frame, string property, XmlReader reader)
        {
            reader.ReadToDescendant("VideoFrame");
            do
            {
                reader.MoveToAttribute("ID");
                if (frame.ID == reader.ReadContentAsInt())
                {
                    switch (property)
                    {
                        case "Subtitle":
                            reader.ReadToFollowing("Subtitle");
                            //reader.w = frame.Subtitle;
                            break;
                        default:
                            break;
                    }
                }
            } while (reader.ReadToNextSibling("VideoFrame"));
        }
    }
}
