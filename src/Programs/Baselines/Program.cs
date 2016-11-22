// Copyright (C) 2012 Zeno Gantner
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using MyMediaLite.Eval.Measures;
using Baselines.Algorithms;
using System.IO;
using CsvHelper;
using System.Linq;
using System.Collections.Generic;
using Baselines.Helper;
using MyMediaLite;

namespace Baselines
{
	
	class Program
	{
		public static void Main (string [] args)
		{
			var program = new Program ();
			program.Run (args);
		}


		void Run (string [] args)
		{
			string model = "/Volumes/Tyr/Projects/UFMG/Baselines/MyMediaLite-3.11/bin/model-100/model-n100.test";
			string validation = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/fold_1/validation.txt";
			string test = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/fold_1/test.txt";

			IList<Checkin> data;
			
			TimeSpan t = Wrap.MeasureTime (delegate () { data = Helper.Utils.ReadCheckins (validation); });
			Console.WriteLine ("Read data: {0} seconds", t.TotalSeconds);

			//var algorithm = new RunBPRMF(model);

			//algorithm.Predict(
			#if DEBUG
    		Console.WriteLine ("Press enter to close...");
			Console.ReadLine ();
			#endif
		}

		void Evaluation ()
		{
			throw new NotImplementedException ();
			//ReciprocalRank.Compute()
		}



	}
}
