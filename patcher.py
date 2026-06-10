import os
import shutil

from unirebuild import UniRebuild, steps
from unirebuild.context import PatcherContext


def fix_stryder_duplicate(context: PatcherContext):
    if not context.platform == "linux":
        return

    src = os.path.join(context.workspace_dir, "Assets/Material/MAT_SB_StryderEtype.mat")
    dst = os.path.join(context.workspace_dir, "Assets/Material/MAT_SB_StryderEtype_0.mat")
    shutil.move(src, dst)
    shutil.move(src + ".meta", dst + ".meta")

patcher = UniRebuild(
    game_name="Frontline",
    workspace_dir="RippedProject"
)

patcher.add_setup_steps([
    steps.ExtractApp(app_path_arg="apk"),
    steps.CopyBundles(),
    steps.RunAssetRipper(),
    steps.ReencodeWavs(),
    steps.DecodeFsbAudio(glob_pattern="Assets/sound/**/*.audioclip.resS"),
    steps.CustomAction(fix_stryder_duplicate),
    steps.SwapFiles({
        "Assets/Scenes/MainUI.unity": "Assets/Scenes/StoreUI.unity",
        "Assets/Scenes/MainUI.unity.meta": "Assets/Scenes/StoreUI.unity.meta",
        "Assets/Scenes/MainUI/LightProbes.asset": "Assets/Scenes/StoreUI/LightProbes.asset",
        "Assets/Scenes/MainUI/LightProbes.asset.meta": "Assets/Scenes/StoreUI/LightProbes.asset.meta",
        "Assets/Scenes/MainUI/LightingData.asset": "Assets/Scenes/StoreUI/LightingData.asset",
        "Assets/Scenes/MainUI/LightingData.asset.meta": "Assets/Scenes/StoreUI/LightingData.asset.meta"
    }),
    steps.DeduplicateAssets(),
    steps.GenerateDeterministicGuids(new_assets_only=False),
    steps.CopyOverrides(overrides_dir="Overrides"),
    steps.ExtractAppIcon(output_path="Assets/Texture2D/app_icon.png"),
    steps.DeleteAssets([
        "Assets/Mesh/Combined Mesh (root_ scene).asset",
        "Assets/Cubemap",
        "Assets/Scenes/GameBoard1/Lightmap-0_comp_light.texture2D"
    ]),
    steps.CopyGitignore(source="unity.gitignore"),
    steps.GitInit(),
    steps.GitCommit(message="AssetRipper", tag="raw-project"),
    steps.ApplyPatches(patches_dir="PrePatches"),
    steps.UnityUpgrade(unity_version="2022.3.62f3", execute_method="AssetUpgrader.UpgradeProject"),
    steps.GenerateDeterministicGuids(new_assets_only=True),
    steps.PopulateTextureSettings(),
    steps.GitCommit(message="Base project", tag="base-project"),
    steps.ApplyPatches(patches_dir="Patches")
])

if __name__ == "__main__":
    patcher.execute()
