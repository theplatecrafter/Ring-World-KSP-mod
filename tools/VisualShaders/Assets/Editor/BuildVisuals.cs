using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildVisuals
{
    // Original, tileable 3-D value/cellular noise. No third-party cloud assets.
    static uint Hash(int x,int y,int z,int salt)
    {unchecked{uint h=(uint)((x&15)*73856093^(y&15)*19349663^(z&15)*83492791^salt);h^=h>>16;h*=0x7feb352d;h^=h>>15;h*=0x846ca68b;return h^(h>>16);}}
    static float R(int x,int y,int z,int s){return (Hash(x,y,z,s)&65535)/65535f;}
    static float Value(Vector3 p)
    {
        int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);Vector3 f=p-new Vector3(x,y,z);f=new Vector3(Mathf.SmoothStep(0,1,f.x),Mathf.SmoothStep(0,1,f.y),Mathf.SmoothStep(0,1,f.z));
        return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(R(x,y,z,1970),R(x+1,y,z,1970),f.x),Mathf.Lerp(R(x,y+1,z,1970),R(x+1,y+1,z,1970),f.x),f.y),Mathf.Lerp(Mathf.Lerp(R(x,y,z+1,1970),R(x+1,y,z+1,1970),f.x),Mathf.Lerp(R(x,y+1,z+1,1970),R(x+1,y+1,z+1,1970),f.x),f.y),f.z);
    }
    public static void Run()
    {
        const int n=64;var noise=new Texture3D(n,n,n,TextureFormat.RGBA32,true){name="Ringworld cloud noise",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};var pixels=new Color[n*n*n];
        for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            Vector3 p=new Vector3(x,y,z)*(16f/n);int ix=Mathf.FloorToInt(p.x),iy=Mathf.FloorToInt(p.y),iz=Mathf.FloorToInt(p.z);float distance=3;
            for(int a=-1;a<=1;a++)for(int b=-1;b<=1;b++)for(int c=-1;c<=1;c++)
            {int xx=ix+a,yy=iy+b,zz=iz+c;var feature=new Vector3(xx+R(xx,yy,zz,91),yy+R(xx,yy,zz,239),zz+R(xx,yy,zz,887));distance=Mathf.Min(distance,Vector3.Distance(p,feature));}
            pixels[(z*n+y)*n+x]=new Color(Value(p),Mathf.Clamp01(1-distance*.8f),Value(p*2),1);
        }
        noise.SetPixels(pixels);noise.Apply(true,false);AssetDatabase.DeleteAsset("Assets/CloudNoise.asset");AssetDatabase.CreateAsset(noise,"Assets/CloudNoise.asset");
        var shape=new Texture3D(n,n,n,TextureFormat.RGBA32,true){name="Ringworld cloud shape",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
        var shapePixels=new Color[pixels.Length];
        for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            var p=new Vector3(x,y,z)*(16f/n);int index=(z*n+y)*n+x;
            float fbm=(Value(p)+.5f*Value(p*2)+.25f*Value(p*4)+.125f*Value(p*8))/1.875f;
            shapePixels[index]=new Color(fbm,pixels[index].g,Value(p*4),Value(p+new Vector3(7,3,11)));
        }
        shape.SetPixels(shapePixels);shape.Apply(true,false);AssetDatabase.DeleteAsset("Assets/CloudShape.asset");AssetDatabase.CreateAsset(shape,"Assets/CloudShape.asset");
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../GameData/NivenRingworld/Assets"));Directory.CreateDirectory(output);
        var bundle=new AssetBundleBuild{assetBundleName="ringworldvisuals",assetNames=new[]{"Assets/Shaders/RingAtmosphere.shader","Assets/Shaders/Precipitation.shader","Assets/Shaders/FullRingAtmosphere.shader","Assets/Shaders/RingWater.shader","Assets/Shaders/RingWaterRefraction.shader","Assets/Shaders/DistantSurface.shader","Assets/Shaders/CloudDeck.shader","Assets/Shaders/GlobalClouds.shader","Assets/Shaders/CloudCoverageProbe.shader","Assets/Shaders/TerrainNight.shader","Assets/Shaders/TerrainTransition.shader","Assets/CloudNoise.asset","Assets/CloudShape.asset"}};
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        var built=BuildPipeline.BuildAssetBundles(output,new[]{bundle},BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(built==null)throw new Exception("Ringworld shader bundle build failed");
        Debug.Log("RINGWORLD VISUAL BUNDLE BUILT: "+output);
    }
}
