using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Addressable management window: Batch rename, add assets with prefix/suffix,
/// create group with Content Update Restriction, generate addressable keys, group dropdown always alphabetical.
/// </summary>
public class AddressableManagerTool : EditorWindow
{
    private enum Tab
    {
        Rename,
        AddAssets,
        GenerateKey
    }

    private Tab selectedTab = Tab.Rename;

    // Rename tab
    private string prefix = "";
    private string suffix = "";
    private int groupIndex = 0;

    // AddAssets tab
    private List<Object> selectedAssets = new List<Object>();
    private Object folderObj = null;
    private int addGroupIndex = 0;
    private bool includeSubfolders = true;
    private string addPrefix = "";
    private string addSuffix = "";

    // Generate Key tab
    private string keyNamespace = "";
    private string keyClassName = "";
    private string outputPath = "Assets/Scripts/Generated/";
    private Dictionary<string, bool> groupSelection = new Dictionary<string, bool>();
    private Vector2 groupSelectionScroll;

    private Vector2 assetListScroll;

    // Group data
    private AddressableAssetSettings settings;
    private string[] groupOptions = new string[0];
    private List<AddressableAssetGroup> sortedGroups = new List<AddressableAssetGroup>();

    // For adding new group
    private string newGroupName = "";

    [MenuItem("Tools/Addressable/Addressable Manager Tool")]
    public static void ShowWindow()
    {
        GetWindow<AddressableManagerTool>("Addressable Manager Tool");
    }

    private void OnEnable()
    {
        RefreshGroups();
        LoadGenerateKeySettings();
    }

    private void OnDisable()
    {
        SaveGenerateKeySettings();
    }

    /// <summary>
    /// Refreshes and sorts group lists for UI dropdown only (does not change asset file!).
    /// </summary>
    private void RefreshGroups()
    {
        settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            // Only affect dropdown: sort all non-readonly groups alphabetically for easier selection
            sortedGroups = settings.groups
                .Where(g => g != null && !g.ReadOnly)
                .OrderBy(g => g.Name)
                .ToList();
            groupOptions = sortedGroups.Select(g => g.Name).ToArray();
            groupIndex = Mathf.Clamp(groupIndex, 0, groupOptions.Length - 1);
            addGroupIndex = Mathf.Clamp(addGroupIndex, 0, groupOptions.Length - 1);

            // Update group selection dictionary for Generate Key tab
            UpdateGroupSelection();
        }
        else
        {
            groupOptions = new string[] { "No AddressableAssetSettings found!" };
        }
    }

    private void UpdateGroupSelection()
    {
        // Add new groups to selection dictionary
        foreach (var group in sortedGroups)
        {
            if (!groupSelection.ContainsKey(group.Name))
            {
                groupSelection[group.Name] = false;
            }
        }

        // Remove groups that no longer exist
        var groupNames = sortedGroups.Select(g => g.Name).ToHashSet();
        var keysToRemove = groupSelection.Keys.Where(k => !groupNames.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            groupSelection.Remove(key);
        }
    }

    private void OnGUI()
    {
        // Header with Groups Window button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open Groups Window", GUILayout.Width(150)))
        {
            OpenAddressableGroupsWindow();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Tabs
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(selectedTab == Tab.Rename, "Batch Rename", EditorStyles.toolbarButton))
            selectedTab = Tab.Rename;
        if (GUILayout.Toggle(selectedTab == Tab.AddAssets, "Add Assets to Group", EditorStyles.toolbarButton))
            selectedTab = Tab.AddAssets;
        if (GUILayout.Toggle(selectedTab == Tab.GenerateKey, "Generate Key Addressable", EditorStyles.toolbarButton))
            selectedTab = Tab.GenerateKey;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Add Group Section
        DrawAddGroupSection();

        EditorGUILayout.Space();

        switch (selectedTab)
        {
            case Tab.Rename:
                DrawRenameTab();
                break;
            case Tab.AddAssets:
                DrawAddAssetsTab();
                break;
            case Tab.GenerateKey:
                DrawGenerateKeyTab();
                break;
        }
    }

    /// <summary>
    /// Opens the Addressable Groups window.
    /// </summary>
    private void OpenAddressableGroupsWindow()
    {
        // Use the standard Unity menu command to open the Addressable Groups window
        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
    }

    /// <summary>
    /// Draws the Add Group UI and handles creating new groups.
    /// </summary>
    private void DrawAddGroupSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Add New Group", GUILayout.Width(110));
        newGroupName = EditorGUILayout.TextField(newGroupName, GUILayout.MinWidth(100));
        if (GUILayout.Button("Add Group", GUILayout.Width(90)))
        {
            AddNewGroup();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Adds a new group to Addressables with Content Update Restriction enabled.
    /// </summary>
    private void AddNewGroup()
    {
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Error", "AddressableAssetSettings not found!", "OK");
            return;
        }

        string trimmed = newGroupName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            EditorUtility.DisplayDialog("Error", "Group name cannot be empty.", "OK");
            return;
        }

        if (settings.groups.Any(g => g != null && g.Name == trimmed))
        {
            EditorUtility.DisplayDialog("Error", "Group name already exists.", "OK");
            return;
        }

        // Add with required schemas for Content Update
        var newGroup = settings.CreateGroup(
            trimmed, false, false, false, null,
            typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
            typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema)
        );
        // Enable Content Update Restriction
        var contentUpdateSchema = newGroup.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema>();
        if (contentUpdateSchema != null)
        {
            contentUpdateSchema.StaticContent = false;
            EditorUtility.SetDirty(contentUpdateSchema);
        }

        AssetDatabase.SaveAssets();
        newGroupName = "";
        RefreshGroups();
        Debug.Log($"Group '{trimmed}' added (Content Update enabled).");
    }

    #region Generate Key Tab

    private void DrawGenerateKeyTab()
    {
        EditorGUILayout.LabelField("Generate Addressable Keys", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Configuration section
        EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
        keyNamespace = EditorGUILayout.TextField("Namespace", keyNamespace);
        keyClassName = EditorGUILayout.TextField("Class Name", keyClassName);

        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("Output Path", outputPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Convert absolute path to relative path
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length) + "/";
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Group selection section
        EditorGUILayout.LabelField("Select Groups to Generate Keys", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All"))
        {
            foreach (var group in sortedGroups)
            {
                groupSelection[group.Name] = true;
            }
        }

        if (GUILayout.Button("Deselect All"))
        {
            foreach (var group in sortedGroups)
            {
                groupSelection[group.Name] = false;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Scrollable group list
        groupSelectionScroll = EditorGUILayout.BeginScrollView(groupSelectionScroll, GUILayout.Height(200));

        foreach (var group in sortedGroups)
        {
            bool isSelected = groupSelection.ContainsKey(group.Name) ? groupSelection[group.Name] : false;
            bool newSelection = EditorGUILayout.ToggleLeft($"{group.Name} ({group.entries.Count} entries)", isSelected);
            groupSelection[group.Name] = newSelection;
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // Generate button
        if (GUILayout.Button("Generate Keys", GUILayout.Height(30)))
        {
            GenerateAddressableKeys();
        }

        EditorGUILayout.Space();

        // Preview section
        int selectedGroupCount = groupSelection.Values.Count(v => v);
        int totalEntryCount = sortedGroups
            .Where(g => groupSelection.ContainsKey(g.Name) && groupSelection[g.Name])
            .Sum(g => g.entries.Count);

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Selected Groups: {selectedGroupCount}");
        EditorGUILayout.LabelField($"Total Entries: {totalEntryCount}");
        EditorGUILayout.LabelField($"Output Files: {selectedGroupCount} files will be generated");

        if (selectedGroupCount > 0)
        {
            EditorGUILayout.LabelField("Example files:");
            var exampleGroups = sortedGroups.Where(g => groupSelection.ContainsKey(g.Name) && groupSelection[g.Name]).Take(3);
            foreach (var group in exampleGroups)
            {
                string groupName = SanitizeClassName(group.Name);
                EditorGUILayout.LabelField($"  {outputPath}{keyClassName.Trim()}.{groupName}.cs", EditorStyles.miniLabel);
            }

            if (selectedGroupCount > 3)
            {
                EditorGUILayout.LabelField($"  ... and {selectedGroupCount - 3} more files", EditorStyles.miniLabel);
            }
        }
    }

    private void GenerateAddressableKeys()
    {
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Error", "AddressableAssetSettings not found!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(keyClassName.Trim()))
        {
            EditorUtility.DisplayDialog("Error", "Class name cannot be empty!", "OK");
            return;
        }

        var selectedGroups = sortedGroups.Where(g => groupSelection.ContainsKey(g.Name) && groupSelection[g.Name]).ToList();
        if (selectedGroups.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please select at least one group!", "OK");
            return;
        }

        // Ensure output directory exists
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // Generate separate file for each group
        int totalKeys = 0;
        foreach (var group in selectedGroups)
        {
            var sb = new StringBuilder();

            // Auto-generated header (similar to Visual Studio)
            sb.AppendLine("//------------------------------------------------------------------------------");
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("//     This code was generated by a tool.");
            sb.AppendLine("//     Runtime Version:4.0.30319.42000");
            sb.AppendLine("//");
            sb.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
            sb.AppendLine("//     the code is regenerated.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("//------------------------------------------------------------------------------");
            sb.AppendLine();

            // Namespace
            if (!string.IsNullOrEmpty(keyNamespace.Trim()))
            {
                sb.AppendLine($"namespace {keyNamespace.Trim()}");
                sb.AppendLine("{");
                sb.AppendLine("    ");
                sb.AppendLine("    ");
            }

            // Class for this group
            string indent = !string.IsNullOrEmpty(keyNamespace.Trim()) ? "    " : "";
            string groupClassName = SanitizeClassName(group.Name);

            sb.AppendLine($"{indent}public sealed partial class {keyClassName.Trim()}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    ");
            sb.AppendLine($"{indent}    public sealed class {groupClassName}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        ");

            foreach (var entry in group.entries)
            {
                string address = entry.address;
                string keyName = SanitizeKeyName(address);

                sb.AppendLine($"{indent}        public const string {keyName} = \"{address}\";");
                sb.AppendLine($"{indent}        ");
                totalKeys++;
            }

            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");

            // Namespace footer
            if (!string.IsNullOrEmpty(keyNamespace.Trim()))
            {
                sb.AppendLine("}");
            }

            // Write file for this group
            string fileName = $"{keyClassName.Trim()}.{groupClassName}";
            string filePath = Path.Combine(outputPath, $"{fileName}.cs");
            File.WriteAllText(filePath, sb.ToString());
        }

        // Refresh AssetDatabase
        AssetDatabase.Refresh();

        Debug.Log($"Addressable keys generated successfully. Files: {selectedGroups.Count}, Total Keys: {totalKeys}");
        EditorUtility.DisplayDialog("Success", $"Addressable keys generated successfully!\n\nFiles: {selectedGroups.Count}\nTotal Keys: {totalKeys}\nLocation: {outputPath}", "OK");
    }

    private string SanitizeClassName(string name)
    {
        // Replace invalid characters with underscores for class names
        var sb = new StringBuilder();

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }

        string result = sb.ToString();

        // Ensure it starts with a letter or underscore
        if (result.Length > 0 && !char.IsLetter(result[0]) && result[0] != '_')
        {
            result = "_" + result;
        }

        // Remove consecutive underscores
        while (result.Contains("__"))
        {
            result = result.Replace("__", "_");
        }

        // Remove trailing underscores
        result = result.TrimEnd('_');

        return string.IsNullOrEmpty(result) ? "DefaultGroup" : result;
    }

    private string SanitizeKeyName(string address)
    {
        // Remove dots and spaces, keep other characters as-is
        string result = address.Replace(".", "").Replace(" ", "");

        // If result is empty or starts with invalid character, add underscore prefix
        if (string.IsNullOrEmpty(result) || (!char.IsLetter(result[0]) && result[0] != '_'))
        {
            result = "_" + result;
        }

        return result;
    }

    private void LoadGenerateKeySettings()
    {
        keyNamespace = EditorPrefs.GetString("AddressableManagerTool.KeyNamespace", "");
        keyClassName = EditorPrefs.GetString("AddressableManagerTool.KeyClassName", "");
        outputPath = EditorPrefs.GetString("AddressableManagerTool.OutputPath", "Assets/Scripts/Generated/");

        // Load group selections
        string groupSelectionData = EditorPrefs.GetString("AddressableManagerTool.GroupSelection", "");
        if (!string.IsNullOrEmpty(groupSelectionData))
        {
            try
            {
                var selections = groupSelectionData.Split(';');
                foreach (var selection in selections)
                {
                    var parts = selection.Split('=');
                    if (parts.Length == 2)
                    {
                        groupSelection[parts[0]] = bool.Parse(parts[1]);
                    }
                }
            }
            catch (System.Exception)
            {
                // If parsing fails, start with empty selection
                groupSelection.Clear();
            }
        }
    }

    private void SaveGenerateKeySettings()
    {
        EditorPrefs.SetString("AddressableManagerTool.KeyNamespace", keyNamespace);
        EditorPrefs.SetString("AddressableManagerTool.KeyClassName", keyClassName);
        EditorPrefs.SetString("AddressableManagerTool.OutputPath", outputPath);

        // Save group selections
        var selections = groupSelection.Select(kvp => $"{kvp.Key}={kvp.Value}");
        EditorPrefs.SetString("AddressableManagerTool.GroupSelection", string.Join(";", selections));
    }

    #endregion

    #region Rename Tab

    private void DrawRenameTab()
    {
        EditorGUILayout.LabelField("Batch Addressable Name Renamer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        prefix = EditorGUILayout.TextField("Prefix", prefix);
        suffix = EditorGUILayout.TextField("Suffix", suffix);

        EditorGUILayout.Space();

        groupIndex = EditorGUILayout.Popup("Target Group", groupIndex, groupOptions);

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply"))
        {
            ApplyBatchRename();
        }
    }

    private void ApplyBatchRename()
    {
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Error", "AddressableAssetSettings not found!", "OK");
            return;
        }

        if (groupIndex < 0 || groupIndex >= sortedGroups.Count)
        {
            EditorUtility.DisplayDialog("Error", "Invalid Addressable Group selected!", "OK");
            return;
        }

        var group = sortedGroups[groupIndex];

        Dictionary<string, string> addressToAssetPath = new Dictionary<string, string>();
        List<string> duplicateAddresses = new List<string>();
        int changedCount = 0;

        foreach (var entry in group.entries)
        {
            var path = AssetDatabase.GUIDToAssetPath(entry.guid);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var newAddress = prefix + fileName + suffix;

            if (addressToAssetPath.ContainsKey(newAddress))
            {
                duplicateAddresses.Add($"- {newAddress}:\n    {addressToAssetPath[newAddress]}\n    {path}");
            }
            else
            {
                addressToAssetPath.Add(newAddress, path);
            }

            if (entry.address != newAddress)
            {
                entry.SetAddress(newAddress);
                changedCount++;
            }
        }

        AssetDatabase.SaveAssets();

        if (duplicateAddresses.Count > 0)
        {
            string msg = "Duplicate Addressable Names detected:\n\n" + string.Join("\n", duplicateAddresses);
            Debug.LogWarning(msg);
            EditorUtility.DisplayDialog("Warning: Duplicates Found!", msg, "OK");
        }
        else
        {
            Debug.Log($"Batch renaming complete. Changed: {changedCount} entries. No duplicate Addressable Names detected.");
        }
    }

    #endregion

    #region Add Assets Tab

    private void DrawAddAssetsTab()
    {
        EditorGUILayout.LabelField("Add Assets to Addressable Group", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        addGroupIndex = EditorGUILayout.Popup("Target Group", addGroupIndex, groupOptions);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefix/Suffix for Addressable Name", EditorStyles.boldLabel);
        addPrefix = EditorGUILayout.TextField("Prefix", addPrefix);
        addSuffix = EditorGUILayout.TextField("Suffix", addSuffix);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add Multiple Assets (Drag & Drop Here)", EditorStyles.boldLabel);

        // Scrollable asset list with drag & drop support
        assetListScroll = EditorGUILayout.BeginScrollView(assetListScroll, GUILayout.Height(110));
        int removeIdx = -1;
        for (int i = 0; i < selectedAssets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            selectedAssets[i] = EditorGUILayout.ObjectField(selectedAssets[i], typeof(Object), false);
            if (GUILayout.Button("X", GUILayout.Width(20)))
                removeIdx = i;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        if (removeIdx >= 0)
            selectedAssets.RemoveAt(removeIdx);

        // Drag & drop zone
        Rect dropArea = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag & Drop assets here", EditorStyles.helpBox);

        // Handle drag & drop
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var draggedObj in DragAndDrop.objectReferences)
                    {
                        if (!selectedAssets.Contains(draggedObj) && AssetDatabase.Contains(draggedObj))
                            selectedAssets.Add(draggedObj);
                    }
                }

                evt.Use();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add All Assets From Folder", EditorStyles.boldLabel);

        folderObj = EditorGUILayout.ObjectField("Folder", folderObj, typeof(DefaultAsset), false);
        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);

        EditorGUILayout.Space();
        if (GUILayout.Button("Add To Group"))
        {
            AddAssetsToGroupAndSetAddress();
        }
    }

    private void AddAssetsToGroupAndSetAddress()
    {
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Error", "AddressableAssetSettings not found!", "OK");
            return;
        }

        if (addGroupIndex < 0 || addGroupIndex >= sortedGroups.Count)
        {
            EditorUtility.DisplayDialog("Error", "Invalid Addressable Group selected!", "OK");
            return;
        }

        var group = sortedGroups[addGroupIndex];

        HashSet<string> guidsToAdd = new HashSet<string>();

        // Collect GUIDs from selected assets
        foreach (var obj in selectedAssets)
        {
            if (obj == null) continue;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                guidsToAdd.Add(guid);
            }
        }

        // Collect GUIDs from folder if selected
        if (folderObj != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(folderObj);
            if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
            {
                var filePaths = Directory.GetFiles(folderPath, "*.*", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (var filePath in filePaths)
                {
                    if (filePath.EndsWith(".meta")) continue;
                    string relPath = filePath.Replace("\\", "/");
                    string guid = AssetDatabase.AssetPathToGUID(relPath);
                    if (!string.IsNullOrEmpty(guid))
                        guidsToAdd.Add(guid);
                }
            }
        }

        Dictionary<string, string> addressToAssetPath = new Dictionary<string, string>();
        List<string> duplicateAddresses = new List<string>();
        int added = 0, existed = 0;

        foreach (var guid in guidsToAdd)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var newAddress = addPrefix + fileName + addSuffix;

            // Check duplicate with other new entries
            if (addressToAssetPath.ContainsKey(newAddress))
            {
                duplicateAddresses.Add($"- {newAddress}:\n    {addressToAssetPath[newAddress]}\n    {path}");
                continue; // skip this one
            }
            else
            {
                addressToAssetPath.Add(newAddress, path);
            }

            var existingEntry = settings.FindAssetEntry(guid);
            if (existingEntry == null)
            {
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.SetAddress(newAddress);
                added++;
            }
            else
            {
                if (existingEntry.parentGroup != group)
                {
                    settings.MoveEntry(existingEntry, group);
                }

                if (existingEntry.address != newAddress)
                {
                    existingEntry.SetAddress(newAddress);
                }

                existed++;
            }
        }

        AssetDatabase.SaveAssets();

        // Clear selected assets after adding to group
        selectedAssets.Clear();
        folderObj = null;

        if (duplicateAddresses.Count > 0)
        {
            string msg = "Duplicate Addressable Names detected (for new assets):\n\n" + string.Join("\n", duplicateAddresses);
            Debug.LogWarning(msg);
            EditorUtility.DisplayDialog("Warning: Duplicates Found!", msg, "OK");
        }
        else
        {
            Debug.Log($"Assets processed: {guidsToAdd.Count} - Added/Moved to group & set address: {added} - Updated existing: {existed} - No duplicate names detected for new assets.");
        }
    }

    #endregion
}