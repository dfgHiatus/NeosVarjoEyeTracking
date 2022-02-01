# NeosVarjoEyeTracking 

A [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod for [Neos VR](https://neos.com/)  
Integrates the Varjo Aero's Eye tracking into NeosVR. Tracks per eye and combined:
- Eye Openness
- Gaze Origin
- Gaze Direction
- Pupil Diameter

Related issue on the Neos Github:
https://github.com/Neos-Metaverse/NeosPublic/issues/3226

## Usage
1. Install [NeosModLoader](https://github.com/zkxs/NeosModLoader).
2. Download the latest release, and extract the zip file's contents to your mods folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\nml_mods` for a default install. You can create it if it's missing, or if you launch the game once with NeosModLoader.
3. Copy the file called "VarjoLib.dll" from your mods folder and paste it into your Neos base directory. This is one folder above your mods, it should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR` for a default install. Additionally, if you are using the Steam build, you can right-click "Neos -> Manage -> Browse Local Files" to get your install directory.
4. Start the game!

If you want to verify that the mod is working you can check your Neos logs, equip an eye-tracking ready avatar, or create an EmptyObject with an AvatarRawEyeData Component (Found under Users -> Common Avatar System -> Face -> AvatarRawEyeData).

Thanks to those who helped me test this!
