using System.Reflection;
using System.Resources;

namespace partial_content_server;

internal static class Assets
{
    private static Assembly Current => typeof(Assets).Assembly;

    // 0-152472
    public static Stream? Img10_Jpg =>
        Current.GetManifestResourceStream("partial_content_server.assets.img10.jpg");
    // 0-214999
    private static Stream? Img11_Jpg =>
        Current.GetManifestResourceStream("partial_content_server.assets.img11.jpg");
    // 0-240678
    private static Stream? Img12_Jpg =>
        Current.GetManifestResourceStream("partial_content_server.assets.img12.jpg");

    public static Stream GetImageStream()
    {
        var stream = new MemoryStream();
        // 0-240678
        Img10_Jpg?.CopyTo(stream);
        // 240679-393150
        Img11_Jpg?.CopyTo(stream);
        // 393151-608149
        Img12_Jpg?.CopyTo(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    public static (int offset, int length) GetImageOffsetAndLength(int? index)
    {
        return index switch
        {
            0 => // first blob
                (0, 240678),
            1 => // second blob
                (240678, 152472),
            2 => // third blob
                (393150, 214999),
            _ => (0, 608149)
        };
    }

    public static Stream? Grade4_Mp3 =>
        Current.GetManifestResourceStream("partial_content_server.assets.grade4.mp3");
}