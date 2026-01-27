using System;
using System.IO;
using MG.MDV.CMD;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MG.MDV
{
    public class HandlerNavigate
    {
        public History  History;
        public string   CurrentPath;

        public Action<float>        ScrollTo;
        public Func<string,Block>   FindBlock;

        //------------------------------------------------------------------------------

        public void SelectPage( string url )
        {
            if( string.IsNullOrEmpty( url ) )
            {
                return;
            }

            // internal link

            if( url.StartsWith( "#" ) )
            {
                var block = FindBlock( url.ToLower() );

                if( block != null )
                {
                    ScrollTo( block.Rect.y );
                }
                else
                {
                    Debug.LogWarning( string.Format( "Unable to find section header {0}", url ) );
                }

                return;
            }

            // relative or absolute link ...

            var newPath = string.Empty;

            if( url.StartsWith( "/" ) )
            {
                newPath = url.Substring( 1 );
            }
            else
            {
                newPath = Utils.PathCombine( Path.GetDirectoryName( CurrentPath ), url );
            }

            // load file

            if (MarkdownCMDUtility.IsCmd(newPath))
            {
                MarkdownCMDUtility.CmdDispose(newPath);
                return;
            }

            var scriptableObjectAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>( newPath );

            if (scriptableObjectAsset != null)
            {
                AssetDatabase.OpenAsset(scriptableObjectAsset);
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<Object>( newPath );

            if( asset != null )
            {
                Selection.activeObject = asset;
            }
            else
            {
                Debug.LogError( string.Format( "Could not find asset {0}", newPath ) );
            }
        }
    }
}
