using System.Windows.Forms.VisualStyles;

namespace Camera_NET
{
    public class RSDevice
    {
        public string DevicePath; // Unic name without ::
        public string Name;
        public bool isIR = true;
        public bool isLeft = true;
    }
}