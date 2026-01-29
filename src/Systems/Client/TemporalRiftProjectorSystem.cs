using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class TemporalRiftProjectorSystem : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;

        private MeshRef meshref;
        private Matrixf matrixf;
        private float counter;

        private IShaderProgram prog;
        private bool shaderReady;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

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

            counter += deltaTime;

            // Similar uniforms to vanilla RiftRenderer
            capi.Render.GLDepthMask(on: false);
            prog.Use();

            try
            {
                prog.Uniform("rgbaFogIn", capi.Render.FogColor);
                prog.Uniform("fogMinIn", capi.Render.FogMin);
                prog.Uniform("fogDensityIn", capi.Render.FogDensity);
                prog.Uniform("rgbaAmbientIn", capi.Render.AmbientColor);
                prog.Uniform("rgbaLightIn", new Vec4f(1f, 1f, 1f, 1f));

                prog.BindTexture2D("primaryFb", capi.Render.FrameBuffers[0].ColorTextureIds[0], 0);
                prog.BindTexture2D("depthTex", capi.Render.FrameBuffers[0].DepthTextureId, 1);
                prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);

                int width = capi.Render.FrameWidth;
                int height = capi.Render.FrameHeight;

                prog.Uniform("counter", counter);
                float bf = 200f + (float)Math.Sin((double)capi.InWorldEllapsedMilliseconds / 24000.0) * 100f;
                prog.Uniform("counterSmooth", bf);
                prog.Uniform("invFrameSize", new Vec2f(1f / width, 1f / height));

                RenderNearbyProjectors();

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

        private void RenderNearbyProjectors()
        {
            var plrPos = capi.World.Player.Entity.Pos;

            int px = (int)Math.Floor(plrPos.X);
            int py = (int)Math.Floor(plrPos.Y);
            int pz = (int)Math.Floor(plrPos.Z);

            const int range = 40;
            const int yRange = 10;

            int riftIndex = 0;

            for (int dx = -range; dx <= range; dx++)
            {
                int x = px + dx;
                for (int dz = -range; dz <= range; dz++)
                {
                    int z = pz + dz;
                    for (int dy = -yRange; dy <= yRange; dy++)
                    {
                        int y = py + dy;

                        var be = capi.World.BlockAccessor.GetBlockEntity(new BlockPos(x, y, z)) as BlockEntityTemporalRiftProjector;
                        if (be == null) continue;

                        riftIndex++;

                        float ox = (float)(x + 0.5 - plrPos.X);
                        float oy = (float)(y + 0.5 + be.OffsetY - plrPos.Y);
                        float oz = (float)(z + 0.5 - plrPos.Z);

                        matrixf.Identity();
                        matrixf.Translate(ox, oy, oz);
                        matrixf.ReverseMul(capi.Render.CameraMatrixOriginf);
                        matrixf.Values[0] = 1f;
                        matrixf.Values[1] = 0f;
                        matrixf.Values[2] = 0f;
                        matrixf.Values[8] = 0f;
                        matrixf.Values[9] = 0f;
                        matrixf.Values[10] = 1f;

                        float size = be.Size;
                        matrixf.Scale(size, size, size);

                        prog.UniformMatrix("modelViewMatrix", matrixf.Values);
                        prog.Uniform("worldPos", new Vec4f(ox, oy, oz, 0f));
                        prog.Uniform("riftIndex", riftIndex);

                        Vec4f tint = ColorUtil.WhiteArgbVec.Clone();
                        float strength = be.ColorStrength;
                        if (strength > 0.001f)
                        {
                            try
                            {
                                var doubles = ColorUtil.Hex2Doubles(be.ColorHex);
                                tint = new Vec4f((float)doubles[0], (float)doubles[1], (float)doubles[2], strength);
                            }
                            catch
                            {
                                tint = new Vec4f(1f, 1f, 1f, strength);
                            }
                        }
                        else
                        {
                            tint = new Vec4f(1f, 1f, 1f, 0f);
                        }

                        prog.Uniform("rgbaTint", tint);

                        capi.Render.RenderMesh(meshref);

                        if (riftIndex >= 40) return;
                    }
                }
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

            meshref?.Dispose();
            base.Dispose();
        }

        public void DisposeRenderer()
        {
            Dispose();
        }
    }
}
