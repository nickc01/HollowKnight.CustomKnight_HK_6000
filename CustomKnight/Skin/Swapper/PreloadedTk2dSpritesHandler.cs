using MonoMod.RuntimeDetour;
using System.Reflection;

namespace CustomKnight.Skin.Swapper
{
    internal class PreloadedTk2dSpritesHandler
    {
        private static Hook _tk2dSpriteAwakeHook;

        internal static void Hook()
        {
            var mi = typeof(tk2dSprite).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (mi != null)
            {
                _tk2dSpriteAwakeHook = new Hook(mi, (Action<Action<tk2dSprite>, tk2dSprite>)Tk2dSprite_Awake);
            }
        }

        internal static void Unhook()
        {
            _tk2dSpriteAwakeHook?.Dispose();
            _tk2dSpriteAwakeHook = null;
        }

        internal static void Enable()
        {
            SwapManager.OnApplySkinUsingProxy += SwapManager_OnApplySkinUsingProxy;
        }

        internal static void Disable()
        {
            SwapManager.OnApplySkinUsingProxy -= SwapManager_OnApplySkinUsingProxy;
        }

        private static void SwapManager_OnApplySkinUsingProxy(object sender, SwapEvent e)
        {
            var marker = e.go.GetComponent<GlobalSwapMarker>();
            var tk2d = e.go.GetComponent<tk2dSprite>();
            if (tk2d != null && marker != null && !marker.optOut)
            {
                Debug.Log("Swapping by path " + marker.originalPath);
                CustomKnight.swapManager.applyGlobalTk2dByPath(marker.originalPath, tk2d);
            }
        }

        private static void Tk2dSprite_Awake(Action<tk2dSprite> orig, tk2dSprite tk)
        {
            orig(tk);
            var path = tk.gameObject.scene.name + "/" + tk.gameObject.GetPath(true);
            var marker = tk.gameObject.GetAddComponent<GlobalSwapMarker>();
            marker.originalPath = path;
        }
    }
}
