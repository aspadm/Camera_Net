namespace Camera_NET
{
    public class RSDevice
    {
        public string DevicePath => Serial + ":" + Suffix; // Unic name without ::
        public string Name;
        public string Serial;
        public bool isIR = true;
        public bool isLeft = true;
        public string Suffix => isIR ? isLeft ? "IR_L" : "IR_R" : "RGB";
    }
}