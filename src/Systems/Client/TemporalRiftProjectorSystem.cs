using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class TemporalRiftProjectorSystem : ModSystem, IRenderer
    {
        private const int Range = 40;
        private const int YRange = 10;
        private const int MaxProjectorsRendered = 40;
        private const int CacheRefreshIntervalMs = 500;
        private const int CacheRefreshMoveThresholdBlocks = 4;

        private ICoreClientAPI capi;

        private MeshRef meshref;
        private Matrixf matrixf;
        private float counter;

        private IShaderProgram prog;
        private bool shaderReady;

        private readonly List<CachedProjector> cachedProjectors = new List<CachedProjector>(32);
        private long lastCacheRefreshMs;
        private int lastCacheRefreshPx;
        private int lastCacheRefreshPy;
        private int lastCacheRefreshPz;

        private readonly Vec4f rgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
        private readonly Vec2f invFrameSize = new Vec2f();
        private readonly Vec4f worldPos = new Vec4f();
        private readonly Vec4f rgbaTint = new Vec4f(1f, 1f, 1f, 0f);

        public override bool ShouldLoad(EnumAppSide forSide) => false;

        public double RenderOrder => 0.051;
        public int RenderRange => 100;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, "alegacyvsquest-temporalriftprojector");

            MeshData mesh = QuadMeshUtil.GetQuad();
            meshref = capi.Render.UploadMesh(mesh);
            matrixf = new Matrixf();

            capi.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        public bool LoadShader()
        {
            try
            {
                prog = capi.Shader.NewShaderProgram();
                prog.AssetDomain = "alegacyvsquest";
                prog.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
                prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);
                capi.Shader.RegisterFileShaderProgram("riftprojector", prog);
                shaderReady = prog.Compile();
                return shaderReady;
            }
            catch
            {
                shaderReady = false;
                return false;
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi?.World?.Player?.Entity?.Pos == null) return;
            if (!shaderReady || prog == null || prog.Disposed || prog.LoadError) return;

            var plrPos = capi.World.Player.Entity.Pos;
            int px = (int)Math.Floor(plrPos.X);
            int py = (int)Math.Floor(plrPos.Y);
            int pz = (int)Math.Floor(plrPos.Z);

            long nowMs = capi.InWorldEllapsedMilliseconds;
            RefreshProjectorCacheIfNeeded(px, py, pz, nowMs);
            if (cachedProjectors.Count == 0) return;

            counter += deltaTime;

            capi.Render.GLDepthMask(on: false);
            prog.Use();

            try
            {
                prog.Uniform("rgbaFogIn", capi.Render.FogColor);
                prog.Uniform("fogMinIn", capi.Render.FogMin);
                prog.Uniform("fogDensityIn", capi.Render.FogDensity);
                prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
                prog.Uniform("rgbaLightIn", rgbaLightIn);

                prog.BindTexture2D("primaryFb", capi.Render.FrameBuffers[0].ColorTextureIds[0], 0);
                prog.BindTexture2D("depthTex", capi.Render.FrameBuffers[0].DepthTextureId, 1);
                prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);

                int width = capi.Render.FrameWidth;
                int height = capi.Render.FrameHeight;

                prog.Uniform("counter", counter);
                float bf = 200f + (float)Math.Sin((double)capi.InWorldEllapsedMilliseconds / 24000.0) * 100f;
                prog.Uniform("counterSmooth", bf);
                invFrameSize.X = 1f / width;
                invFrameSize.Y = 1f / height;
                prog.Uniform("invFrameSize", invFrameSize);

                RenderCachedProjectors(plrPos);

                counter = GameMath.Mod(counter + deltaTime, (float)Math.PI * 200f);
            }
            catch
            {
            }
            finally
            {
                prog.Stop();
                capi.Render.GLDepthMask(on: true);
            }
        }

        private void RefreshProjectorCacheIfNeeded(int px, int py, int pz, long nowMs)
        {
            bool timeExpired = nowMs - lastCacheRefreshMs >= CacheRefreshIntervalMs;
            bool movedFarEnough =
                Math.Abs(px - lastCacheRefreshPx) >= CacheRefreshMoveThresholdBlocks
                || Math.Abs(py - lastCacheRefreshPy) >= CacheRefreshMoveThresholdBlocks
                || Math.Abs(pz - lastCacheRefreshPz) >= CacheRefreshMoveThresholdBlocks;

            if (!timeExpired && !movedFarEnough) return;

            RefreshProjectorCache(px, py, pz, nowMs);
        }

        private void RefreshProjectorCache(int px, int py, int pz, long nowMs)
        {
            cachedProjectors.Clear();

            var world = capi?.World;
            var blockAccessor = world?.BlockAccessor;
            if (world == null || blockAccessor == null) return;

            int chunkSize = GlobalConstants.ChunkSize;

            int minCx = DivFloor(px - Range, chunkSize);
            int maxCx = DivFloor(px + Range, chunkSize);
            int minCy = DivFloor(py - YRange, chunkSize);
            int maxCy = DivFloor(py + YRange, chunkSize);
            int minCz = DivFloor(pz - Range, chunkSize);
            int maxCz = DivFloor(pz + Range, chunkSize);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    for (int cz = minCz; cz <= maxCz; cz++)
                    {
                        var chunk = blockAccessor.GetChunk(cx, cy, cz);
                        if (chunk == null || chunk.Disposed) continue;

                        var blockEntities = chunk.BlockEntities;
                        if (blockEntities == null || blockEntities.Count == 0) continue;

                        foreach (var kvp in blockEntities)
                        {
                            if (kvp.Value is not BlockEntityTemporalRiftProjector be) continue;

                            var pos = kvp.Key;
                            if (pos == null) continue;

                            int dx = pos.X - px;
                            if (dx < -Range || dx > Range) continue;

                            int dz = pos.Z - pz;
                            if (dz < -Range || dz > Range) continue;

                            int dy = pos.Y - py;
                            if (dy < -YRange || dy > YRange) continue;

                            float distSq = (float)(dx * dx + dy * dy + dz * dz);

                            float strength = be.ColorStrength;
                            float tr = 1f;
                            float tg = 1f;
                            float tb = 1f;
                            float ta = 0f;

                            if (strength > 0.001f)
                            {
                                ta = strength > 1f ? 1f : strength;
                                if (!TryHexToRgb(be.ColorHex, out tr, out tg, out tb))
                                {
                                    tr = 1f;
                                    tg = 1f;
                                    tb = 1f;
                                }
                            }

                            cachedProjectors.Add(new CachedProjector
                            {
                                X = pos.X,
                                Y = pos.Y,
                                Z = pos.Z,
                                OffsetY = be.OffsetY,
                                Size = be.Size,
                                TintR = tr,
                                TintG = tg,
                                TintB = tb,
                                TintA = ta,
                                DistSq = distSq
                            });
                        }
                    }
                }
            }

            cachedProjectors.Sort(CachedProjectorComparer.Instance);

            lastCacheRefreshMs = nowMs;
            lastCacheRefreshPx = px;
            lastCacheRefreshPy = py;
            lastCacheRefreshPz = pz;
        }

        private static int DivFloor(int value, int divisor)
        {
            int div = value / divisor;
            int rem = value % divisor;
            if (rem != 0 && value < 0) div--;
            return div;
        }

        private void RenderCachedProjectors(EntityPos plrPos)
        {
            int limit = cachedProjectors.Count < MaxProjectorsRendered ? cachedProjectors.Count : MaxProjectorsRendered;
            for (int i = 0; i < limit; i++)
            {
                var p = cachedProjectors[i];

                float ox = (float)(p.X + 0.5 - plrPos.X);
                float oy = (float)(p.Y + 0.5 + p.OffsetY - plrPos.Y);
                float oz = (float)(p.Z + 0.5 - plrPos.Z);

                matrixf.Identity();
                matrixf.Translate(ox, oy, oz);
                matrixf.ReverseMul(capi.Render.CameraMatrixOriginf);
                matrixf.Values[0] = 1f;
                matrixf.Values[1] = 0f;
                matrixf.Values[2] = 0f;
                matrixf.Values[8] = 0f;
                matrixf.Values[9] = 0f;
                matrixf.Values[10] = 1f;

                float size = p.Size;
                matrixf.Scale(size, size, size);

                prog.UniformMatrix("modelViewMatrix", matrixf.Values);
                worldPos.X = ox;
                worldPos.Y = oy;
                worldPos.Z = oz;
                worldPos.W = 0f;
                prog.Uniform("worldPos", worldPos);
                prog.Uniform("riftIndex", i + 1);

                rgbaTint.X = p.TintR;
                rgbaTint.Y = p.TintG;
                rgbaTint.Z = p.TintB;
                rgbaTint.W = p.TintA;
                prog.Uniform("rgbaTint", rgbaTint);

                capi.Render.RenderMesh(meshref);
            }
        }

        private static bool TryHexToRgb(string hex, out float r, out float g, out float b)
        {
            r = 1f;
            g = 1f;
            b = 1f;

            if (string.IsNullOrWhiteSpace(hex)) return false;

            int start = hex[0] == '#' ? 1 : 0;
            if (hex.Length - start < 6) return false;

            if (!TryParseHexByte(hex, start + 0, out byte rb)) return false;
            if (!TryParseHexByte(hex, start + 2, out byte gb)) return false;
            if (!TryParseHexByte(hex, start + 4, out byte bb)) return false;

            r = rb / 255f;
            g = gb / 255f;
            b = bb / 255f;
            return true;
        }

        private static bool TryParseHexByte(string s, int index, out byte value)
        {
            value = 0;
            if (index + 1 >= s.Length) return false;

            int hi = HexToInt(s[index]);
            int lo = HexToInt(s[index + 1]);
            if (hi < 0 || lo < 0) return false;

            value = (byte)((hi << 4) | lo);
            return true;
        }

        private static int HexToInt(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
            return -1;
        }

        private struct CachedProjector
        {
            public int X;
            public int Y;
            public int Z;
            public float OffsetY;
            public float Size;
            public float TintR;
            public float TintG;
            public float TintB;
            public float TintA;
            public float DistSq;
        }

        private sealed class CachedProjectorComparer : IComparer<CachedProjector>
        {
            public static readonly CachedProjectorComparer Instance = new CachedProjectorComparer();

            public int Compare(CachedProjector x, CachedProjector y)
            {
                return x.DistSq.CompareTo(y.DistSq);
            }
        }

        public override void Dispose()
        {
            try
            {
                capi.Event.ReloadShader -= LoadShader;
            }
            catch
            {
            }

            try
            {
                capi?.Event?.UnregisterRenderer(this, EnumRenderStage.AfterBlit);
            }
            catch
            {
            }

            meshref?.Dispose();
            base.Dispose();
        }

        public void DisposeRenderer()
        {
            Dispose();
        }
    }
}
