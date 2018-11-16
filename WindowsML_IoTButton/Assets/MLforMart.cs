using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.AI.MachineLearning;
namespace WindowsML_IoTButton
{
    
    public sealed class MLforMartInput
    {
        public ImageFeatureValue data; // BitmapPixelFormat: Bgra8, BitmapAlphaMode: Premultiplied, width: 227, height: 227
    }
    
    public sealed class MLforMartOutput
    {
        public TensorString classLabel; // shape(-1,1)
        public IList<Dictionary<string,float>> loss;
    }
    
    public sealed class MLforMartModel
    {
        private LearningModel model;
        private LearningModelSession session;
        private LearningModelBinding binding;
        public static async Task<MLforMartModel> CreateFromStreamAsync(IRandomAccessStreamReference stream)
        {
            MLforMartModel learningModel = new MLforMartModel();
            learningModel.model = await LearningModel.LoadFromStreamAsync(stream);
            learningModel.session = new LearningModelSession(learningModel.model);
            learningModel.binding = new LearningModelBinding(learningModel.session);
            return learningModel;
        }
        public async Task<MLforMartOutput> EvaluateAsync(MLforMartInput input)
        {
            binding.Bind("data", input.data);
            var result = await session.EvaluateAsync(binding, "0");
            var output = new MLforMartOutput();
            output.classLabel = result.Outputs["classLabel"] as TensorString;
            output.loss = result.Outputs["loss"] as IList<Dictionary<string,float>>;
            return output;
        }
    }
}
