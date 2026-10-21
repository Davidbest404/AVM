using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature: пикселизаци€ финального кадра.
/// Ќе вли€ет на физику, Raycast'ы, Transform'ы Ч только на картинку.
/// </summary>
public class PixelateRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("ћатериал с шейдером AVM/PixelateEffect.")]
        public Material material;

        [Tooltip(" огда примен€ть. –екомендую AfterRenderingPostProcessing.")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [Tooltip("–азмер пиксел€ (1 Ч выключено, 2Ц4 Ч заметно, 6+ Ч очень грубо).")]
        [Range(1f, 16f)] public float pixelSize = 3f;

        [Tooltip(" оличество цветовых уровней (2Ц64). 32 Ч почти незаметно, 8 Ч PS1-стиль.")]
        [Range(2f, 64f)] public float colorLevels = 32f;
    }

    public Settings settings = new Settings();

    private PixelatePass _pass;

    public override void Create()
    {
        if (settings.material == null) return;
        _pass = new PixelatePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass != null && settings.material != null)
            renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    private class PixelatePass : ScriptableRenderPass
    {
        private Material _material;
        private Settings _settings;
        private RTHandle _tempRT;
        private const string ProfilerTag = "PixelatePass";

        public PixelatePass(Settings s)
        {
            _settings = s;
            _material = s.material;
            renderPassEvent = s.renderPassEvent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            // FilterMode.Point Ч критично, чтобы не было Ђм€гкихї пикселей из-за билинейной фильтрации
            RenderingUtils.ReAllocateIfNeeded(
                ref _tempRT,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_PixelateTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (_material == null) return;

            // ќбновл€ем параметры материала
            _material.SetFloat("_PixelSize", Mathf.Max(1f, _settings.pixelSize));
            _material.SetFloat("_ColorLevels", Mathf.Max(2f, _settings.colorLevels));

            CommandBuffer cmd = CommandBufferPool.Get(ProfilerTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTag)))
            {
                var source = data.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, source, _tempRT, _material, 0);
                Blitter.BlitCameraTexture(cmd, _tempRT, source);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            _tempRT?.Release();
            _tempRT = null;
        }

        public void Dispose()
        {
            _tempRT?.Release();
            _tempRT = null;
        }
    }
}