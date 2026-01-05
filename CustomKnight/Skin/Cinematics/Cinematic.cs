
using UnityEngine.Video;
using TeamCherry.Cinematics;

namespace CustomKnight
{
    /// <summary>
    /// Class that defines a replacable Cinamatic
    /// </summary>
    public class Cinematic
    {
        /// <summary>
        /// Name of the current Cinematic
        /// </summary>
        public string ClipName { get; private set; }
        internal VideoClip OriginalVideo = null;
        internal EmbeddedCinematicVideoPlayer player = null;
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="ClipName"></param>
        public Cinematic(string ClipName)
        {
            this.ClipName = ClipName;
        }

    }
}