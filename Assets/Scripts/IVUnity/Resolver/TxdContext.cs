using System.Collections.Generic;
using RageLib.Textures;

namespace IVUnity.Resolver
{
    public enum TxdLoadState : byte
    {
        NotLoaded = 0,
        Loading   = 1,
        Loaded    = 2,
        Failed    = 3,
    }

    /// <summary>
    /// Per-TXD slot — engine analogue of a CTxdStore entry. Owns the parsed
    /// <see cref="TextureFile"/> (rage::pgDictionary&lt;grcTexture&gt;) plus a parent reference,
    /// a ref count for lifetime management, and a load-state machine.
    ///
    /// Lifecycle:
    ///   • <see cref="TxdStore.Acquire"/> creates the slot if missing, drives it through
    ///     NotLoaded → Loading → (Loaded | Failed), and bumps RefCount. Recurses into the
    ///     parent (from IDE txdp) and acquires the same way — engine equivalent of
    ///     FUN_008e07a0 linking the parent slot at load completion.
    ///   • <see cref="TxdStore.Release"/> decrements RefCount. The store evicts slots whose
    ///     RefCount reached zero some time ago (LRU window).
    ///
    /// Texture lookup (<see cref="FindTexture"/>) walks self → parent like
    /// pgDictionary::Get does, so a model's materials see the union of every dictionary in
    /// the chain.
    /// </summary>
    public sealed class TxdSlot
    {
        public string         Name { get; }
        public TxdLoadState   State;
        public TextureFile    Dictionary;     // null until Loaded
        public TxdSlot        Parent;         // assigned at load; never reassigned afterwards
        public int            RefCount;       // store-managed; bumped by Acquire, dropped by Release
        public int            FailureCount;   // capped retry counter
        public float          LastUsedTime;   // for LRU eviction once RefCount hits 0

        public TxdSlot(string name) { Name = name; State = TxdLoadState.NotLoaded; }

        public Texture FindTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;
            for (var s = this; s != null; s = s.Parent)
            {
                if (s.Dictionary == null) continue;
                var tex = s.Dictionary.FindTextureByName(textureName);
                if (tex != null) return tex;
            }
            return null;
        }

        /// <summary>
        /// Self → parent → … flattened into the array shape RageLib's
        /// <c>ModelGenerator.GenerateModel(textures)</c> consumes. Skips slots whose
        /// dictionary failed to load (null) so the linear search doesn't hit them.
        /// </summary>
        public TextureFile[] ToChainArray()
        {
            var list = new List<TextureFile>(2);
            for (var s = this; s != null; s = s.Parent)
            {
                if (s.Dictionary != null) list.Add(s.Dictionary);
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        /// <summary>Returns every slot in the chain (self → parent → …) for store ref counting.</summary>
        public TxdSlot[] ChainSlots()
        {
            var list = new List<TxdSlot>(2);
            for (var s = this; s != null; s = s.Parent) list.Add(s);
            return list.ToArray();
        }
    }

}
