namespace Wrapper;

internal class Program
{
    private static void Main(string[] args)
    {
        var aprint = new apRemoteNet.APRemote_Net();
        aprint.ConnectServer("127.0.0.1", 1025);
        aprint.OpenPrintDialog(true);
    }
}