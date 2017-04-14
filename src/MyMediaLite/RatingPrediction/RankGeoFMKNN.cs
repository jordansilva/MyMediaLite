// Copyright (C) 2016 Jordan Silva
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
using System.Collections.Generic;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

namespace MyMediaLite.RatingPrediction
{

	/// <summary>Weahter based enhancement of Rank-GeoFM</summary>
	/// <remarks>
	///   <para>
	///     We use the fast learning method proposed by Hu et al. (alternating least squares, ALS),
	///     and we use a global parameter to give observed values higher weights.
	///   </para>
	///   <para>
	///     Literature:
	///     <list type="bullet">
	///       <item><description>
	///         Xutao Li, Gao Cong, Xiao-Li Li, Tuan-Anh Nguyen Pham, and Shonali Krishnaswamy. 2015. 
	///         Rank-GeoFM: A Ranking based Geographical Factorization Method for Point of Interest Recommendation. 
	///         ACM SIGIR 2015. 
	///         http://dx.doi.org/10.1145/2766462.2767722
	///       </description></item>
	///     </list>
	///   </para>
	///   <para>
	///     This recommender supports incremental updates.
	///   </para>
	/// </remarks>
	public class WPOI : RankGeoFM
	{
		#region Fields

		/// <summary>
		/// Climate Matrix CL ∈ R |WeatherDimensions|x|K|
		/// saving the weather similarities/probabilities between POI's
		/// Used to parametrize the feature classes of the specific weather feature
		/// </summary>
		private Matrix<double> F;

		/// <summary>
		/// "Model Paramaeter L^(2) used to model the user's own preference. L(2) ∈ R|L|×K" [1]
		/// The weather-popularity-score that models whether or not a location
		/// is popular in a specific weather feature class
		/// </summary>
		private Matrix<double> L_2;

		/// <summary>
		/// "Model Paramaeter L^(3) used to model the user's own preference. L(3) ∈ R|L|×K" [1]
		/// The influence between two feature classes.
		/// L(3) softens the borders between the particular feature classes.
		/// </summary>
		private Matrix<double> L_3;

		/// <summary>
		/// Matrix WI ∈ R is introduced for storing the probability 
		/// that a weather feature class c is influenced by feature class c'.
		/// </summary>
		private Matrix<double> WI;

		private int [,,] UICF;

		#endregion

		#region Properties

		/// <summary>
		/// |FCf| defines the size of the bin for the current weather feature.
		/// The 'bins' is the interval value that weather feature can be normalized.
		/// </summary>
		public int Bins { get; set; }

		#endregion

		public WPOI () : base ()
		{
			MaxIterations = 7000;
			Bins = 20;

		}

		/// <summary>
		/// Normalizes the feature ~ eq (1).
		/// </summary>
		/// <returns>Normalized feature value</returns>
		/// <param name="feature">value</param>
		/// <param name="min">Minimum feature</param>
		/// <param name="max">Max feature</param>
		float normalizeFeature (float feature, float min, float max)
		{
			return ((feature - min) * (Bins - 1)) / (max - min);
		}


		/// <summary>
		/// Creates the user item context matrix.
		/// </summary>
		void CreateUserItemContextMatrix ()
		{
			UICF = new int [totalUsers, totalItems, Bins]; //User Item Context Frequency Matrix
			Parallel.For (0, Bins, opts, index => CreateUserItemContextFrequency (index));
		}

		void CreateUserItemContextFrequency (int index)
		{
			//TODO: Implement UICF creation here!
		}

		/// <summary>
		/// Computes the matrix WI that is the probability of 
		/// that weather feature class c is influenced by feature class c'.
		/// </summary>
		void ComputeMatrixWI ()
		{
			WI = CreateMatrix.Dense<double> (Bins, Bins);
			Parallel.For (0, Bins, opts, index => CalculatePropabilityMatrixWI (index));
			WI.NormalizeRows (1.0);
		}

		///
		void CalculatePropabilityMatrixWI (int index)
		{
			for (int i = 0; i < Bins; i++) {
				if (index != i) {
					double num = 0.0;
					double d1 = 0.0;
					double d2 = 0.0;

					for (int user = 1; user <= totalUsers; user++) {
						for (int item = 0; item < totalItems; item++) {
							num += UICF [user, item, index] * UICF [user, item, i];
							d1 += Math.Pow (UICF [user, item, index], 2);
							d2 += Math.Pow (UICF [user, item, i], 2);
						}
					}

					var div = Math.Sqrt (d1) * Math.Sqrt (d2);
					if (Math.Abs (div) == 0)
						WI [index, i] = 0;
					else
						WI [index, i] = num / div;
				} else
					WI [index, i] = 0;
			}
		}


		double ComputeRecommendationScoreFeature (int user_id, int item_id, DateTime time)
		{
			return 0;
			//return ComputeRecommendationScoreFeature (user_id, item_id, timeFeatureClassMapper [time]);
		}

		double ComputeRecommendationScoreFeature (int user_id, int item_id, int feature)
		{
			//part (1) yul + F_c * L2_l
			var rankgeofm_score = ComputeRecommendationScore (user_id, item_id);
			var time_score = rankgeofm_score + (F.Row (feature) * L_2.Row (item_id));

			//part (2) L3_l * sum_c(WIcc * fc)
			var weather_feature = L_3.Row (item_id) * (WI.Row (feature) * F);

			return time_score + weather_feature;
		}

		public override void Train ()
		{
			base.Train ();
		}

		protected override void Iterate (IList<int> rating_indices)
		{
			//RankGeo-FM Iteration (u,l) ∈ D1
			base.Iterate (rating_indices);

			//Context Iteration (u,l,c) ∈ D2
			timed_ratings.AllUsers.Shuffle ();
			Parallel.ForEach (timed_ratings.AllUsers, opts, user => IterationWeather (user));
		}

		void IterationWeather (int user)
		{
			throw new NotImplementedException ();
		}
	}
}
