# FrontlinePatcher

This is a tool for patching the Titanfall: Frontline game to fix bugs and add Frontline Emulator/OpenTOY support.

## How to use

1. Download the latest release from the [releases page](https://github.com/TFReclaimed/FrontlinePatcher/releases).
2. Extract the contents of the downloaded ZIP file.
3. Install [Apktool](https://apktool.org/docs/install) and the [Java JDK](https://adoptium.net/temurin/releases).
4. Install [Android Studio](https://developer.android.com/studio) and the Android SDK Build-Tools from the SDK Manager.
5. Obtain a supported Titanfall: Frontline APK file and place it in the same directory as the patcher.
6. Execute `./FrontlinePatcher <apk_file> <output_apk_file>` on Linux or `FrontlinePatcher.exe <apk_file> <output_apk_file>` on Windows, replacing `<apk_file>` with the path to your Titanfall: Frontline APK file and `<output_apk_file>` with the desired output file name.

Save the created `frontline.keystore` file to a secure location, you will need it in the future to update the game.
