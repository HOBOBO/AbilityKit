using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class BattleMapDTO
    {
        public int Id;
        public string Name;
        public MapBoundsDTO Bounds;
        public MapWalkableAreaDTO[] WalkableAreas;
        public MapSpawnPointDTO[] SpawnPoints;
        public MapCollisionObjectDTO[] CollisionObjects;
    }

    [Serializable]
    public sealed class MapWalkableAreaDTO
    {
        public int Id;
        public string Name;
        public MapVector3DTO Center;
        public MapVector3DTO Size;
    }

    [Serializable]
    public sealed class MapBoundsDTO
    {
        public MapVector3DTO Center;
        public MapVector3DTO Size;
    }

    [Serializable]
    public sealed class MapSpawnPointDTO
    {
        public int Id;
        public int TeamId;
        public MapVector3DTO Position;
        public float YawDegrees;
    }

    [Serializable]
    public sealed class MapCollisionObjectDTO
    {
        public int Id;
        public string Name;
        public string ObjectType;
        public string ShapeType;
        public MapVector3DTO Position;
        public MapVector3DTO RotationEuler;
        public MapVector3DTO Size;
        public float Radius;
        public float Height;
        public int CollisionLayer;
        public bool BlocksMovement;
        public string ProjectileResponse;
        public bool GenerateView;
    }

    [Serializable]
    public sealed class MapVector3DTO
    {
        public float X;
        public float Y;
        public float Z;
    }
}
