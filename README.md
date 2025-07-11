# Motivations / Inspirations

Sourcing parts from JLCPCB can be a bit of a pain when you don't have the footprint, the symbol, or the model. 

If you're a user of KiCAD you're in luck as there's a nice project [easyeda2kicad.py](https://github.com/uPesy/easyeda2kicad.py) which will offline convert and pull models for you, and there's another script which integrates this script into KiCAD.

If you're an Altium user, you're stuck either running this script and importing via KiCAD import, or exporting the Altium files from EasyEDA and copying them manually into a footprint.

I used easyeda2kicad as a reference for this project as well as AtliumLibraryLoader script for reference on Altium APIs to manipulate adding parts to libraries

# Usage

Using the extension is pretty straight forward once it is installed, there will be a new Menu option `EasyEDA Loader` while a SchDoc is active, this will open a Modal Dialog which prompts the LCSC Part number e.g. "C2040". 

Hitting OK will automatically create `EasyEDA.pcblib` and `EasyEDA.schlib` if they don't already exist in `Documents/AltiumEE`, create the footprint, download the 3d model, create the symbol, add part info, map the footprint to the symbol, then place the component into the active schematic at the bottom left.

# Comparisons

Left EasyEDA, Right Altium after import

## Symbol

![Comparison of EasyEDA Symbol](/Assets/Compare-Symbol.png)

## Footprint

![Comparison of EasyEDA Footprint](/Assets/Compare-Footprint.png)

## 3D Model

![Comparison of EasyEDA 3D](/Assets/Compare-3D.png)

## Part Info

![Comparison of EasyEDA Part Info](/Assets/PartInfo-EEL.PNG)

# Building

You shouldn't need anything special to build, just .NET 4.8, Language v8.0, and probably assembly references to Altium's internal libraries.

The following Assembly references were made and can be found in 

```
C:\Program Files\Altium\AD24\System
C:\Program Files\Altium\AD24\System\DotNet\DevExpress.Wpf
```

```
Altium.Controls.dll
Altium.SDK.dll
Altium.SDK.Interfaces.dll
DevExpress.Utils.v22.1.dll
DevExpress.XtraEditors.v22.1.dll
```

# Standalone

The standalone version is a simple WPF app that draws the primitives to a Canvas and was mainly used to validate without having to repeatedly re-launch Altium. Unfortunately doesn't load the step file, but will load the raw obj model, you can also use it to manually save the step or obj.

# Installation

Copy to your Offline Setup Altium Designer Extensions directory so that it can be installed from Extensions

Or extract contents to for example:

`C:\ProgramData\Altium\Altium Designer {08BC8A67-180A-4240-B39B-AF5998437998}\Extensions\EasyEDA-Loader`

And register it to your ExtensionsRegistry.xml with contents near the bottom:

```
 <Item HRID="EasyEDA-Loader" Guid="8035C261-E5FE-403B-A9B5-9ABFFB6E0EF5">
    <Path>C:\ProgramData\Altium\Altium Designer {08BC8A67-180A-4240-B39B-AF5998437998}\Extensions\EasyEDA-Loader</Path>
    <Status>0</Status>
    <VaultGuid></VaultGuid>
    <CreatedBy>Altium, Inc.</CreatedBy>
    <CategoryGuid>793A1F67-0B22-4E01-A5DE-3176A1E8C60D</CategoryGuid>
    <CategoryName></CategoryName>
    <ReadMe></ReadMe>
    <Help></Help>
    <Requirements></Requirements>
    <Title>EasyEDA-Loader</Title>
    <ShortDescription>EasyEDA-Loader</ShortDescription>
    <LongDescription>EasyEDA-Loader</LongDescription>
    <SmallImage></SmallImage>
    <LargeImage></LargeImage>
    <Version>1.0.0.0</Version>
    <VersionGuid>7042BC82-F870-462D-86AF-B158AC75C490</VersionGuid>
    <ReleasedDate>45495.4140277778</ReleasedDate>
    <ReleaseNotes></ReleaseNotes>
    <DateInstalled>45838.7675816088</DateInstalled>
    <PlatformVersions>
      <DXP BuildNumber="1.0.16.41"/>
      <EDP BuildNumber="10.0.16.41"/>
      <MaxDXP BuildNumber="0.0.0.0"/>
      <MaxEDP BuildNumber="0.0.0.0"/>
    </PlatformVersions>
  </Item>
```

## Known Issues
The 3D model is not places *quite* right, something is still different from the reported translation and the actual. See [EeFootprint3dModel](/EasyEDA-Loader/FootprintShapes/EeFootprint3dModel.cs) for more information and how and where it retrieves model info from.
