using System;
using System.Collections.Generic;

namespace Baselines.Algorithms
{
	public interface IBaseline
	{
		String Name();
		IList<Tuple<int, float>> Predict(int user, IList<int> items);
		Tuple<int, float> Predict (int user, int item);
	}
}