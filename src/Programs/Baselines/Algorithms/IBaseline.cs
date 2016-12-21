using System;
using System.Collections.Generic;
using MyMediaLite.IO;

namespace Baselines.Algorithms
{
	public interface IBaseline
	{
		string Name();

		void LoadModel (string filename);
		void Train (string training, ItemDataFileFormat file_format);
		void SetParameter (string name, object value);
		IList<Tuple<int, float>> Predict(int user, IList<int> items);
		Tuple<int, float> Predict (int user, int item);
	}
}