#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Kit
{
    public class GUILayerDrawer
    {
        private List<IGUILayer> _layers = new List<IGUILayer>();

        private List<IGUILayer> _layersToAdd = new List<IGUILayer>();
        private List<IGUILayer> _layersToRemove = new List<IGUILayer>();

        public void AddLayer(IGUILayer layer)
        {
            this._layersToAdd.Add(layer);
        }

        public void RemoveLayer(IGUILayer layer)
        {
            this._layersToRemove.Add(layer);
        }

        public void Draw(Rect rect, object userData)
        {
            for (var i = 0; i < this._layers.Count; i++)
            {
                IGUILayer layer = this._layers[i];
                layer.Draw(rect, userData);
            }

            if (this._layersToAdd.Count > 0)
            {
                this._layers.AddRange(this._layersToAdd);
                this._layersToAdd.Clear();
            }

            if (this._layersToRemove.Count > 0)
            {
                for (var i = 0; i < this._layersToRemove.Count; i++)
                {
                    IGUILayer layer = this._layersToRemove[i];
                    this._layers.Remove(layer);
                }

                this._layersToRemove.Clear();
            }

            this._layers.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public void Clear()
        {
            this._layers.Clear();
            this._layersToAdd.Clear();
            this._layersToRemove.Clear();
        }
    }
}
#endif