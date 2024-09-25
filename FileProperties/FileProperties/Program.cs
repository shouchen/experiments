using Microsoft.WindowsAPICodePack.Shell;

class Program
{
    static void Main(string[] args)
    {
        string filePath = @"C:\Users\shouc\OneDrive\Desktop\sbux.jpg";
        string tags = "tag1;tag2;tag3";

        SetFileTags(filePath, tags);
    }

    static void SetFileTags(string filePath, string tags)
    {
        using (ShellFile shellFile = ShellFile.FromFilePath(filePath))
        {
            shellFile.Properties.System.Keywords.Value = tags.Split(';');
        }
    }
}