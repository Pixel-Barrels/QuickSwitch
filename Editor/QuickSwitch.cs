#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Threading;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

public class QuickSwitch : EditorUtility
{
#if UNITY_EDITOR_WIN
    /// <summary>
    /// Creates a symbolic link in the filesystem. Requires Vista or higher. Requires administrator access.
    /// </summary>
    /// <param name="lpSymlinkFileName">Symlink file name</param>
    /// <param name="lpTargetFileName">Target file names</param>
    /// <param name="dwFlags">Symbolic Link flags</param>
    /// <returns></returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool CreateSymbolicLink(
        string lpSymlinkFileName,
        string lpTargetFileName,
        SymbolicLink dwFlags);

    enum SymbolicLink
    {
        File = 0,
        Directory = 1
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool RemoveDirectory(string path);
#endif

    [MenuItem("Tools/Quick Switch/Switch to Android...")]
    static void SwitchToAndroid() => SwitchTo(BuildTarget.Android);

    [MenuItem("Tools/Quick Switch/Switch to Windows...")]
    static void SwitchToWindows() => SwitchTo(BuildTarget.StandaloneWindows64);
    
    [MenuItem("Tools/Quick Switch/Switch to Linux...")]
    static void SwitchToLinux() => SwitchTo(BuildTarget.StandaloneLinux64);
    
    [MenuItem("Tools/Quick Switch/Switch to UWP...")]
    static void SwitchToUWP() => SwitchTo(BuildTarget.WSAPlayer);
    
    [MenuItem("Tools/Quick Switch/Switch to Nintendo Switch...")]
    static void SwitchToNX() => SwitchTo(BuildTarget.Switch);
    
    [MenuItem("Tools/Quick Switch/Switch to Xbox One...")]
    static void SwitchToXboxOne() => SwitchTo(BuildTarget.XboxOne);
    
    [MenuItem("Tools/Quick Switch/Switch to PS4...")]
    static void SwitchToPS4() => SwitchTo(BuildTarget.PS4);
    
    [MenuItem("Tools/Quick Switch/Switch to PS5...")]
    static void SwitchToPS5() => SwitchTo(BuildTarget.PS5);

    [MenuItem("Tools/Quick Switch/Switch to WebGL")]
    static void SwitchToWeb() => SwitchTo(BuildTarget.WebGL);
    
    static void SwitchTo(BuildTarget target)
    {
        float progress = 0;

        void EnableProgressBar()
        {
            EditorUtility.DisplayProgressBar("Switching platform...", "Copying cache", progress);
            Thread.Sleep(500); //Make sure we show the progress bar.
        }
        
        void DisableProgressBar() => EditorUtility.ClearProgressBar();
        
#if UNITY_EDITOR_LINUX
        Debug.LogWarning("The Linux environment sometimes throws the error 'Directory not empty', which causes this tool to fail.");        
#endif
        
        EnableProgressBar();
        
        if (target == 0)
        {
            Debug.LogWarning("You didn't select a valid Target Platform!");
            
            DisableProgressBar();
            return;
        }

        progress += .1f;
        EnableProgressBar();

        var currentPlatform = EditorUserBuildSettings.activeBuildTarget;

        if (currentPlatform == target)
        {
            Debug.LogWarning("You selected the current platform as the Target Platform!");
            
            DisableProgressBar();
            return;
        }

        progress += .1f;
        EnableProgressBar();

        // Don't switch when compiling
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("Could not switch platform because Unity is compiling!");
            
            DisableProgressBar();
            return;
        }

        progress += .1f;
        EnableProgressBar();
        
        // Don't switch while playing
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Could not switch platform because Unity is in Play Mode!");
            
            DisableProgressBar();
            return;
        }

        progress += .1f;
        EnableProgressBar();
        
        Debug.Log("Switching platform from " + currentPlatform + " to " + target);

        //save current Library folder state
        if (Directory.Exists("Library-" + currentPlatform))
            DirectoryClear("Library-" + currentPlatform);

        progress += .1f;
        EnableProgressBar();

        if (!Directory.Exists("Library-" + target))
            DirectoryCopy("Library", "Library-" + target, true);

        progress += .1f;
        EnableProgressBar();

#if UNITY_EDITOR_WIN
        RemoveDirectory("Library");
        CreateSymbolicLink("Library", $"Library-{target}", SymbolicLink.Directory);

        progress += .1f;
        EnableProgressBar();
#else
        //restore new target Library folder state
        if (Directory.Exists("Library-" + target))
        {
            try
            {
                DirectoryClear("Library");

                progress += .1f;
                EnableProgressBar();
                
                MoveDirectory("Library-" + target, "Library");
            }
            catch (Exception e)
            {
                Debug.LogError("Copy failed! Aborting...");
                Debug.LogError($" {e.Source} - {e.Message} - {e.InnerException}");
                
                DisableProgressBar();
                return;
            }
        }

        progress += .1f;
        EnableProgressBar();
#endif
        
        var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);
        EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, target);

        progress += .2f;
        EnableProgressBar();
        
        Debug.Log("Platform switched to " + target);
        
        DisableProgressBar();
    }

    static void DirectoryClear(string folderName)
    {
        DirectoryInfo dir = new DirectoryInfo(folderName);

        foreach (FileInfo fi in dir.GetFiles())
        {
            if (IsFileBlacklisted(fi.Name))
                continue;

            fi.Delete();
        }

        foreach (DirectoryInfo di in dir.GetDirectories())
        {
            DirectoryClear(di.FullName);
            di.Delete(true);
        }
    }

    static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);
        DirectoryInfo[] dirs = dir.GetDirectories();

        // If the source directory does not exist, throw an exception.
        if (!dir.Exists)
            throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);

        // If the destination directory does not exist, create it.
        if (!Directory.Exists(destDirName))
            Directory.CreateDirectory(destDirName);
        
        // Get the file contents of the directory to copy.
        FileInfo[] files = dir.GetFiles();

        foreach (FileInfo file in files)
        {
            if (IsFileBlacklisted(file.Name))
                continue;

            // Create the path to the new copy of the file.
            string tempPath = Path.Combine(destDirName, file.Name);

            // Copy the file.
            file.CopyTo(tempPath, false);
        }

        // If copySubDirs is true, copy the subdirectories.
        if (copySubDirs)
        {    
            foreach (DirectoryInfo subDir in dirs)
            {
                // Create the subdirectory.
                string tempPath = Path.Combine(destDirName, subDir.Name);

                // Copy the subdirectories.
                DirectoryCopy(subDir.FullName, tempPath, true);
            }
        }
    }

    /// <summary>
    ///  Moves a file or a directory and its contents to an existing location (by doing a Recursive Files Move)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="target"></param>
    private static void MoveDirectory(string source, string target)
    {
        var sourcePath = source.TrimEnd('\\', ' ');
        var targetPath = target.TrimEnd('\\', ' ');
        var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)
            .GroupBy(s => Path.GetDirectoryName(s));

        foreach (var folder in files)
        {
            var targetFolder = folder.Key.Replace(sourcePath, targetPath);
            Directory.CreateDirectory(targetFolder);

            foreach (var file in folder)
            {
                if (IsFileBlacklisted(Path.GetFileName(file)))
                    continue;

                var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
                if (File.Exists(targetFile)) File.Delete(targetFile);
                File.Move(file, targetFile);
            }
        }
        Directory.Delete(source, true);
    }

    /// <summary>
    /// Checks for files that shouldn't be moved or copied (mainly files that are being used by some Unity process)
    /// </summary>
    /// <returns></returns>
    private static bool IsFileBlacklisted(string filename)
    {
        string[] blacklist = { "ShaderCache.db", "shadercompiler-UnityShaderCompiler.exe" };
        return blacklist.Contains(filename);
    }
}

#endif