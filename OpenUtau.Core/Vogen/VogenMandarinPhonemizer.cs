using Microsoft.ML.OnnxRuntime;
using OpenUtau.Api;

namespace OpenUtau.Core.Vogen {
    [Phonemizer("Vogen Chinese Mandarin Phonemizer", "VOGEN ZH", language: "ZH")]
    public class VogenMandarinPhonemizer : VogenBasePhonemizer {
        private static InferenceSession? g2p;
        private static InferenceSession? prosody;

        public VogenMandarinPhonemizer() {
            g2p ??= Onnx.getInferenceSession(Data.VogenRes.g2p_man, OnnxRunnerChoice.CPU);
            G2p = g2p;
            prosody ??= Onnx.getInferenceSession(Data.VogenRes.po_man, OnnxRunnerChoice.CPU);
            Prosody = prosody;
        }
        protected override string LangPrefix => "man:";

        protected override string[] Romanize(string[] lyrics) {
            return BaseChinesePhonemizer.Romanize(lyrics);
        }
    }
}
