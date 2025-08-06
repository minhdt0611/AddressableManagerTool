# Addressable Manager Tool

A comprehensive Unity Editor tool for managing Addressable Assets with batch rename, group management, and key generation features.

## Features

- **Batch Rename**: Rename multiple addressable assets with prefix/suffix
- **Add Assets to Groups**: Drag & drop or folder-based asset addition to addressable groups
- **Generate Addressable Keys**: Auto-generate C# constants for addressable keys
- **Group Management**: Create new addressable groups with Content Update Restriction
- **Alphabetical Sorting**: Groups are always sorted alphabetically in UI

## Requirements

- Unity 2021.3 or later
- Addressables package (com.unity.addressables) 1.19.19 or later

## Installation

### Method 1: Unity Package Manager (Git URL)
1. Open Unity
2. Go to `Window → Package Manager`
3. Click the `+` button and select `Add package from git URL`
4. Enter: `https://github.com/yourusername/AddressableManagerTool.git`

### Method 2: Download and Import
1. Download the latest release from [GitHub Releases](https://github.com/yourusername/AddressableManagerTool/releases)
2. Import the `.unitypackage` file via `Assets → Import Package → Custom Package`

### Method 3: Manual Installation
1. Download or clone this repository
2. Copy the entire folder to your project's `Packages/` directory
3. Rename the folder to `com.yourname.addressable-manager-tool`

## Usage

### Opening the Tool
Go to `Tools → Addressable → Addressable Manager Tool`

### Features Overview

#### 1. Batch Rename Tab
- Select a target group
- Set prefix and suffix for addressable names
- Click "Apply" to rename all assets in the group

#### 2. Add Assets to Group Tab
- Choose target group
- Set prefix/suffix for new addressable names
- Drag & drop assets or select from folder
- Include/exclude subfolders option

#### 3. Generate Key Addressable Tab
- Configure namespace and class name
- Select output path for generated files
- Choose which groups to generate keys for
- Generates separate files for each group with constants

### Generated Key Usage Example

```csharp
using YourNamespace;

public class ExampleUsage : MonoBehaviour
{
    void Start()
    {
        // Using generated keys
        Addressables.LoadAssetAsync<GameObject>(AddressableKeys.UI.MainMenuPanel);
        Addressables.LoadAssetAsync<Texture2D>(AddressableKeys.Textures.PlayerAvatar);
    }
}
```

## Configuration

The tool automatically saves your settings:
- Generate Key configurations (namespace, class name, output path)
- Group selections for key generation

## Troubleshooting

### Common Issues
1. **"AddressableAssetSettings not found!"**
   - Make sure Addressables package is installed
   - Initialize Addressables via `Window → Asset Management → Addressables → Groups`

2. **Generated files not compiling**
   - Check that namespace and class names are valid C# identifiers
   - Ensure output path is within the Assets folder

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

[MIT License](LICENSE.md)

## Support

For issues and feature requests, please use the [GitHub Issues](https://github.com/yourusername/AddressableManagerTool/issues) page.