using SubExt.Model;
using System;
using System.Collections.ObjectModel;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Storage;

namespace SubExt.ViewModel
{
    public class ViewModel
    {
        public ObservableCollection<VideoFrame> VideoFrames { get; set; }

        public void GetVideoFrames()
        {
            if (ApplicationData.Current.LocalSettings.Containers.Count > 0)
            {
                GetSavedVideoFrames();
            }
            else
            {
                GetDefaultVideoFrames();
            }
        }
        public void GetDefaultVideoFrames()
        {
            ObservableCollection<VideoFrame> a = new ObservableCollection<VideoFrame>();

            // Items to collect
            a.Add(new VideoFrame() { TimeStamp = new TimeSpan(0) });

            VideoFrames = a;
            //MessageBox.Show("Got accomplishments from default");
        }

        public void GetSavedVideoFrames()
        {
            ObservableCollection<VideoFrame> a = new ObservableCollection<VideoFrame>();

            foreach (Object o in ApplicationData.Current.LocalSettings.Containers.Values)
            {
                a.Add((VideoFrame)o);
            }

            VideoFrames = a;
            //MessageBox.Show("Got accomplishments from storage");
        }
    }
}
