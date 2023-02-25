using System;
using System.Linq;
using OpenCvSharp;
using DlibDotNet;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Point = DlibDotNet.Point;

namespace detection
{
    public sealed class Detection : MonoBehaviour
    {
        [Serializable] private class DetectParam: UnityEvent<Vec3f, Point[]> { }
        [SerializeField] private DetectParam onDetect = new();
        
        private bool _isRunning;
        
        private static FrontalFaceDetector _faceDetector;
        private static ShapePredictor _shapePredictor;

        private static int _cantFindFrames;
        private static Rectangle _face = Rectangle.Empty;
        
        private void Start()
        {
            _faceDetector = Dlib.GetFrontalFaceDetector();
            _shapePredictor = ShapePredictor.Deserialize("Assets/scripts/shape_predictor_68_face_landmarks.dat");
        }
        private void Update()
        {
            if (!_isRunning) return;
            GetLandmarks();
        }
        
        public void StartDetection() => _isRunning = true;
        
        public void StopDetection() => _isRunning = false;

        public void GetLandmarks()
        {
            if(!_isRunning) return;
            CameraManager.Instance.GetFrame(out var mat);
            
            var width = mat.Width;
            var height = mat.Height;
            var elemSize = mat.ElemSize();
            
            var array = new byte[width * height * elemSize];
            Marshal.Copy(mat.Data, array, 0, array.Length);
            var image = Dlib.LoadImageData<RgbPixel>(
                array,
                (uint)height, 
                (uint)width,
                (uint)(width * elemSize));
                
            var faces = _faceDetector.Operator(image);

            if (faces.Length == 0)
            {
                if (_cantFindFrames >= 10)
                {
                    return;
                }
                if (_face == Rectangle.Empty)
                {
                    return;
                }

                _cantFindFrames++;
            }
            else
            {
                _face = faces[0];
                _cantFindFrames = 0;
            }
            
            var points = new Point[68];

            var shapes = _shapePredictor.Detect(image, _face);
            Parallel.For(0, 68, i =>
            {
                points[i] = shapes.GetPart((uint)i);
                Cv2.Circle(mat,
                    new OpenCvSharp.Point(points[i].X, points[i].Y),
                    2,
                    Scalar.Green,
                    -1);//*/
            });//*/
            var row = image.Rows;
            var col = image.Columns;
            var vec = SolvePnP.Solve(points, row, col);
            var rot = new Vec3f(
                (float)Math.Sin(vec[1].x), 
                (float)Math.Sin(vec[1].y), 
                (float)Math.Sin(vec[1].z));
            
            var eyeRatio = CoordinateParser.GetEyeRatio(points);
            var pupils = CoordinateParser.GetPupil(points, mat, eyeRatio.ToArray());
            
            onDetect.Invoke(rot, points);
        }
    }
}
