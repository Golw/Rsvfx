using UnityEngine;
using Intel.RealSense;

namespace Rsvfx
{
    public sealed class PointCloudBaker : MonoBehaviour
    {
        #region Editable attributes

        [SerializeField] RsFrameProvider _colorSource = null;
        [SerializeField] RsFrameProvider _pointSource = null;
        [Space]
        [SerializeField] RenderTexture _colorMap = null;
        [SerializeField] RenderTexture _positionMap = null;

        [SerializeField, HideInInspector] ComputeShader _compute = null;

        #endregion

        #region Private objects

        (FrameQueue color, FrameQueue point) _frameQueue;
        DepthConverter _converter;

        #endregion

        #region MonoBehaviour implementation

        void Start()
        {
            _frameQueue = (new FrameQueue(1), new FrameQueue(1));
            _converter = new DepthConverter(_compute);

            _colorSource.OnNewSample += OnNewColorSample;
            _pointSource.OnNewSample += OnNewPointSample;
        }

        void OnDestroy()
        {
            _frameQueue.color?.Dispose();
            _frameQueue.point?.Dispose();
            _frameQueue = (null, null);

            _converter?.Dispose();
            _converter = null;
        }

        void Update()
        {
            // Try dequeuing and load a color frame.
            VideoFrame cf;
            if (_frameQueue.color.PollForFrame(out cf))
                using (cf) _converter.LoadColorData(cf);

            // Try dequeuing and load a point frame.
            Points pf;
            if (_frameQueue.point.PollForFrame(out pf))
                using (pf) _converter.LoadPointData(pf);

            // Bake them.
            _converter.UpdateAttributeMaps(_colorMap, _positionMap);
        }

        #endregion

        #region Frame provider callbacks

        void OnNewColorSample(Frame frame)
        {
            using (var cf = RetrieveColorFrame(frame))
                if (cf != null) _frameQueue.color.Enqueue(cf);
        }

        void OnNewPointSample(Frame frame)
        {
            using (var pf = RetrievePointFrame(frame))
                if (pf != null) _frameQueue.point.Enqueue(pf);
        }

        #endregion

        #region Frame query methods

        Frame RetrieveColorFrame(Frame frame)
        {
            using (var profile = frame.Profile)
            {
                if (profile.Stream == Stream.Color &&
                    profile.Format == Format.Rgba8 &&
                    profile.Index == 0)
                return frame;
            }

            if (frame.IsComposite)
            {
                using (var fs = FrameSet.FromFrame(frame))
                {
                    foreach (var f in fs)
                    {
                        var ret = RetrieveColorFrame(f);
                        if (ret != null) return ret;
                        f.Dispose();
                    }
                }
            }

            return null;
        }

        Frame RetrievePointFrame(Frame frame)
        {
            if (frame.Is(Extension.Points)) return frame;

            if (frame.IsComposite)
            {
                using (var fs = FrameSet.FromFrame(frame))
                {
                    foreach (var f in fs)
                    {
                        var ret = RetrievePointFrame(f);
                        if (ret != null) return ret;
                        f.Dispose();
                    }
                }
            }

            return null;
        }

        #endregion
    }
}
