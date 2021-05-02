
using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace Beat360fyerPlugin
{

    internal class Config
    {
        public static Config Instance { get; set; }
        public virtual bool ShowGenerated360 { get; set; } = true;
        public virtual bool ShowGenerated90 { get; set; } = true;
        public virtual bool EnableWallGenerator { get; set; } = true;
        public virtual int LimitRotations360 { get; set; } = 28;
        public virtual int LimitRotations90 { get; set; } = 2;
        public virtual string BasedOn { get; set; } = "Standard"; // Can be Standard,OneSaber,NoArrows


        /// <summary>
        /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
        /// </summary>
        public virtual void OnReload()
        {
            // Do stuff after config is read from disk.
        }

        /// <summary>
        /// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
        /// </summary>
        public virtual void Changed()
        {
            // Do stuff when the config is changed.
        }

        /// <summary>
        /// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
        /// </summary>
        public virtual void CopyFrom(Config other)
        {
            // This instance's members populated from other
        }
    }
}
