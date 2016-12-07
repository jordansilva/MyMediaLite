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
using System.Linq;
using System.Collections.Generic;
using MyMediaLite.Data;
using MyMediaLite.DataType;
using MyMediaLite.Helper;
using System.IO;
using MyMediaLite.IO;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Geographical factorization method proposed by Li et al.</summary>
	/// <remarks>
	///   <para>
	///     We use the fast learning method proposed by Hu et al. (alternating least squares, ALS),
	///     and we use a global parameter to give observed values higher weights.
	///   </para>
	///   <para>
	///     Literature:
	///     <list type="bullet">
	///       <item><description>
	/// 		Xutao Li, Gao Cong, Xiao-Li Li, Tuan-Anh Nguyen Pham, and Shonali Krishnaswamy. 2015. 
	/// 		Rank-GeoFM: A Ranking based Geographical Factorization Method for Point of Interest Recommendation. 
	/// 		ACM SIGIR 2015. 
	/// 		http://dx.doi.org/10.1145/2766462.2767722
	///       </description></item>
	///     </list>
	///   </para>
	///   <para>
	///     This recommender supports incremental updates.
	///   </para>
	/// </remarks>
	public class RankGeoFMBackup : TimeAwareRatingPredictor
	{

		#region Fields

		const string FILENAME_DISTANCE_MATRIX = @"DistanceMatrix.bin.RankGeoFM";
		const string FILENAME_DISTANCE_MATRIX_IDX = @"DistanceMatrixIndex.bin.RankGeoFM";
		const string FILENAME_U1 = @"U1.bin.RankGeoFM";
		const string FILENAME_U2 = @"U2.bin.RankGeoFM";
		const string FILENAME_L1 = @"L1.bin.RankGeoFM";
		const string FILENAME_W = @"W.bin.RankGeoFM";

		Matrix<double> distanceMatrix;
		Matrix<int> distanceMatrixIndex;
		Matrix<double> W;

		/// <summary>
		/// "Model Paramaeter U^(1) used to model the user's own preference. U(1) ∈ R|U|×K" [1]
		/// </summary>
		private Matrix<double> U_1;

		/// <summary>
		/// On the other hand, we introduce one extra latent factor matrix U(2) ∈ R|U|×K for users, 
		/// and employ U(2) to model the interaction between users and POIs for incorporating the geographical influence.
		/// </summary>
		private Matrix<double> U_2;

		/// <summary>
		/// "Model Paramaeter L^(1) used to model the user's own preference. L(1) ∈ R|L|×K" [1]
		/// </summary>
		private Matrix<double> L_1;

		/// <summary>
		/// "Model Paramaeter L^(2) used to model the user's own preference. L(2) ∈ R|L|×K" [1]
		/// </summary>
		private double [,] L_2;

		/// <summary>
		/// "Model Paramaeter L^(3) used to model the user's own preference. L(3) ∈ R|L|×K" [1]
		/// </summary>

		private double [,] L_3;

		/// <summary>
		/// Maps Database UserId to Matrix UserId in U1-U4
		/// </summary>
		Dictionary<int, int> idMapperUsers;

		/// <summary>
		/// Maps Database LocationId to Matrix LocationId in L1
		/// </summary>
		Dictionary<int, int> idMapperLocations;

		#endregion

		#region Properties

		/// <summary>
		/// Hyperparameter k_1 is the number of neighbors considered for Geographical influence
		/// </summary>
		public uint K_1 { get { return k_1; } set { k_1 = value; } }
		uint k_1 = 500;

		/// <summary>K is the number of dimensions of latent space Θ</summary> 
		public uint K { get; set; }

		/// <summary>
		/// Hyperparameter ε initialized as in [1]
		/// The Epsilon is named as maginal in the original Matlab code.
		/// </summary>
		public float Epsilon { get; set; }

		/// <summary>
		/// Hyperparameter C initialized as in [1] eq(5), eq(6) and eq(7)
		/// The C is named as C in original Matlab code.
		/// </summary>
		public float C { get; set; }

		/// <summary>
		/// Hyperparameter Learning rate γ initialized as in [1]
		/// The Gamma is named as alpha in the original Matlab code.
		/// </summary>
		public float Gamma { get; set; }

		/// <summary>
		/// Hyperparameter Alpha initialize as in [1] eq(7)
		/// "As a result, tuning the hyperparameter α can balance the contributions of user-preference and geographical influence scores to the final recommendation score." [1]
		/// "We find that Rank-GeoFM perfoms the best at α = 0.2 for POI recommendation on both data, and performs the best at α=0.1 for time-aware POI recommendation on both data. " [1]
		/// The Alpha is named as FactorInf in the original Matlab code.
		/// </summary>
		public float Alpha { get; set; }

		/// <summary>Number of maximum iterations</summary>
		public uint MaxIterations { get; set; }

		/// <summary>The POIs items.</summary>
		public IList<POI> Items { get; set; }

		#endregion

		///
		public RankGeoFMBackup ()
		{
			K = 100;
			C = 1.0f;
			Epsilon = 0.3f;
			Gamma = 0.0001f;
			Alpha = 0.2f;
			MaxIterations = 1000;
		}

		/// <summary>
		/// Initialize configurations and data for training.
		/// </summary>
		void Initialize ()
		{
			Console.WriteLine ("{0} Computing distance matrix", DateTime.Now);
			ComputeDistanceMatrix ();

			Console.WriteLine ("{0} Creating preferences matrix", DateTime.Now);
			CreatePreferenceMatrix ();

			Console.WriteLine ("{0} Creating weighted matrix", DateTime.Now);
			CreateWeightedMatrix ();
		}

		/// <summary>
		/// Initialize the distance matrix between all items.
		/// Store only K nearest neighbors of each item.
		/// </summary>
		void ComputeDistanceMatrix ()
		{
			if (Items == null)
				throw new Exception ("Items can not be null");

			if (File.Exists (FILENAME_DISTANCE_MATRIX)) {
				distanceMatrix = (Matrix<double>)FileSerializer.Deserialize (FILENAME_DISTANCE_MATRIX);
				distanceMatrixIndex = (Matrix<int>)FileSerializer.Deserialize (FILENAME_DISTANCE_MATRIX_IDX);
			} else {
				distanceMatrix = new Matrix<double> (Items.Count + 1, k_1);
				distanceMatrixIndex = new Matrix<int> (Items.Count + 1, k_1);

				var i = 1;
				var opts = new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32 (Math.Ceiling ((Environment.ProcessorCount * 0.95) * 1.0)) };
				Parallel.ForEach (Items, opts, item => CalculateDistancesItem (item, ref i));
				SaveDistanceMatrix ();
			}
		}

		void CreatePreferenceMatrix ()
		{
			if (File.Exists (FILENAME_U1)) {
				U_1 = (Matrix<double>)FileSerializer.Deserialize (FILENAME_U1);
				U_2 = (Matrix<double>)FileSerializer.Deserialize (FILENAME_U2);
				L_1 = (Matrix<double>)FileSerializer.Deserialize (FILENAME_L1);
			} else {
				//TODO: Check MaxUserID or AllUsers.Count/AllItems.Count
				U_1 = new Matrix<double> (Ratings.AllUsers.Count + 1, K);
				U_2 = new Matrix<double> (Ratings.AllUsers.Count + 1, K);
				L_1 = new Matrix<double> (Ratings.AllItems.Count + 1, K);

				idMapperUsers = new Dictionary<int, int> ();
				idMapperLocations = new Dictionary<int, int> ();

				InitMatrixNormal (Ratings.AllUsers, ref U_1, ref idMapperUsers, K);
				InitMatrixNormal (Ratings.AllUsers, ref U_2, ref idMapperUsers, K);
				InitMatrixNormal (Ratings.AllItems, ref L_1, ref idMapperLocations, K);
			}
		}

		/// <summary>
		/// Creates the W matrix that contains probabilities that user visits POI l when l' has been visited.
		/// </summary>
		void CreateWeightedMatrix ()
		{
			if (File.Exists (FILENAME_W)) {
				W = (Matrix<double>)FileSerializer.Deserialize (FILENAME_W);
			} else {
				W = new Matrix<double> (Items.Count + 1, k_1);

				var count = 1;
				var opts = new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32 (Math.Ceiling ((Environment.ProcessorCount * 0.95) * 1.0)) };
				Parallel.For (0, distanceMatrix.NumberOfRows, opts, index => ComputeItemWeightedMatrix (index, ref count));

				SaveWeightedMatrix ();
			}
		}

		/// <summary>
		/// Computes the weighted matrix.
		/// </summary>
		/// <param name="i">The index.</param>
		/// <param name="count">Count.</param>
		void ComputeItemWeightedMatrix (int i, ref int count)
		{
			//for (int i = 0; i < distanceMatrix.NumberOfRows; i++) {
			if (distanceMatrix.GetRow (i).Count < K)
				throw new Exception (string.Format ("Number of nearest neighbors ({0}) is less than K={1}",
												   distanceMatrix.GetRow (i).Count, K));

			var values = distanceMatrix.GetRow (i);
			var reg_values = values.Select (x => (x < 0.5 && x > 0) ? 0.5 : x).ToList ();
			var total = reg_values.Select (x => (1.0 / x)).Sum ();
			reg_values = reg_values.Select (x => ((1.0 / x) / total)).ToList ();
			W.SetRow (i, reg_values);

			count++;
			if ((count % 10000) == 0)
				Console.WriteLine ("{0} - {1}", DateTime.Now, count);
			//}
		}

		void InitMatrixNormal (IList<int> ids, ref Matrix<double> M, ref Dictionary<int, int> mapper, uint k)
		{
			Normal normalDist = new Normal (0.0, 0.01);
			int i = 0;
			foreach (int id in ids) {
				for (int j = 0; j < k; j++) {
					M [i, j] = normalDist.Sample ();
					mapper [id] = i;
				}
				i++;
			}
		}

		void CalculateDistancesItem (POI item, ref int count)
		{
			var dist = new List<double> ();
			foreach (var item2 in Items) {
				if (item.Id == item2.Id)
					dist.Add (double.MaxValue);
				else
					dist.Add (DistanceHelper.Distance (item.Coordinates.Latitude, item.Coordinates.Longitude,
													   item2.Coordinates.Latitude, item2.Coordinates.Longitude));
			}

			var sorted = dist.Select ((x, i) => new KeyValuePair<double, int> (x, i))
							 .OrderBy (x => x.Key)
							 .Take ((int)k_1).ToList ();
			distanceMatrix.SetRow (item.Id, sorted.Select (x => x.Key).ToList ());
			distanceMatrixIndex.SetRow (item.Id, sorted.Select (x => x.Value).ToList ());
			dist = null;

			count++;
			if ((count % 10000) == 0) {
				Console.WriteLine ("{0} - {1}", DateTime.Now, count);
				SaveDistanceMatrix ();
			}
		}

		/// <summary>
		/// Gets the nearest neighbors of the POI.
		/// </summary>
		/// <param name="id">Id</param>
		/// <returns>The nearest neighbors.</returns>
		public IList<double> GetNearestNeighborsItem (int id)
		{
			return distanceMatrix.GetRow (id);
		}

		///
		public override float Predict (int user_id, int item_id)
		{
			throw new NotImplementedException ();
		}

		///
		public override float Predict (int user_id, int item_id, DateTime time)
		{
			throw new NotImplementedException ();
		}

		///
		public override void Train ()
		{
			Initialize ();
			InitializeLossWeight ();
			//TODO: Shuffle data

			uint iter = 1;
			int numUsers = Ratings.AllUsers.Count;
			int numItems = Ratings.AllItems.Count;
			double threshold = C * Alpha;
			double best_precision = 0.0;

			while (iter <= MaxIterations) {
				//Latent Factors User
				for (int i = 0; i < numUsers; i++) {
					for (int j = 0; j < K_1; j++) {
						var norm = U_2.GetRow (i).EuclideanNorm ();
						if (norm > threshold) {
							var row = U_2.GetRow (i);
							row = DataType.VectorExtensions.Multiply (row, Alpha * C);
							row = DataType.VectorExtensions.Divide (row, norm);
							U_2.SetRow (i, row);
						}
					}
				}

				//Latent Factors Item
				var F_G = new Matrix<double> (numItems + 1, K);
				for (int i = 0; i < numItems; i++) {
					for (int j = 0; j < K_1; j++) {
						var weight = W [i, j];
						var row = F_G.GetRow (i);
						var row2 = DataType.VectorExtensions.Multiply (L_1.GetRow (idMapperLocations [j]),
						                                               weight);
						DataType.VectorExtensions.Add (row, row2);
						F_G.SetRow (i, row);
					}
				}

				//FIXME: Test performance new!
				//var metric = test_performance_new(self.path, U_1, L_1, U_2, F_G, 10);

				var metric = new double [2];
				var precision = metric [0];
				var recall = metric [1];

				Console.WriteLine ("Precision: {0} - Recall: {1}", precision, recall);

				if (precision > best_precision) {
					best_precision = precision;
					SaveWeightedMatrix ();
				}

				var U_1_pre = U_1;
				var U_2_pre = U_2;
				var L_1_pre = L_1;

				//FIXME: 
				//var UL = U_1 * L_1';
				//var UFG = U_2 * F_G';
				var UL = 0;
				var UFG = 0;

				for (int i = 0; i < Ratings.AllUsers.Count; i++) {
					
				}

				iter++;
			}

		}

		double [] lossWeight;
		double lossWeightTotal;

		void InitializeLossWeight ()
		{
			lossWeight = new double [Ratings.AllItems.Count + 1];
			lossWeightTotal = 0.0;

			for (int i = 0; i < Ratings.AllItems.Count; i++) {
				lossWeight [i] = lossWeightTotal + 1 / i;
				lossWeightTotal = lossWeightTotal + (1 / i);
			}
		}

		/// <summary>
		/// Indicator the specified statement.
		/// </summary>
		/// <param name="statement">If set to <c>true</c> statement.</param>
		int Indicator (bool statement) {
			return (statement) ? 1 : 0;
		}

		/// <summary>
		/// Ranking Incompatibility function eq(1)
		/// Measures the number of POIs that are incorrectly ranked higher than l for user u.
		/// </summary>
		/// <param name="x_ul">Users frequency of visits to POI ℓ</param>
		/// <param name="x_ul2">Users frequency of visits to POI ℓ'</param>
		/// <param name="y_ul">Recommendation score of POI ℓ</param>
		/// <param name="y_ul2">Recommendation score of POI ℓ'</param>
		int Incompatibility (double x_ul, double x_ul2, double y_ul, double y_ul2) {
			
			return Indicator(x_ul > x_ul2) * Indicator(y_ul < (y_ul2 * Epsilon));
		}

		/// <summary>
		/// Computs the ranking incompatibility from incompFunction into a loss eq(2)
		/// </summary>
		/// <param name="r">Rating incompatibility.</param>
		private double E (int r)
		{
			double sum = 0;
			for (int i = 1; i <= r; i++) {
				sum += 1 / i;
			}
			return sum;
		}

		/// <summary>
		/// Computes the recommendation score eq (4)
		/// </summary>
		/// <returns>The recommendation score.</returns>
		/// <param name="user">user</param>
		/// <param name="item">location</param>
		private double ComputeRecommendationScore (int user, int item)
		{
			var user_pref = ComputeUserPreferenceScore (user, item);
			var geo_pref = ComputeGeographicPreferenceScore (user, item);

			return user_pref + geo_pref;
		}

		double ComputeUserPreferenceScore (int user, int location)
		{
			var ind_user = idMapperUsers [user];
			var ind_loc = idMapperLocations [location];

			var u_row = U_1.GetRow (ind_user);
			var l_row = L_1.GetRow (ind_loc);

			var score = DataType.VectorExtensions.ScalarProduct (u_row, l_row);
			return score;
		}

		double ComputeGeographicPreferenceScore (int user, int location)
		{
			var ind_user = idMapperUsers [user];
			var ind_loc = idMapperLocations [location];

			var u_row = U_2.GetRow (ind_user);

			IList<double> sum = new List<double> ();
			for (int i = 0; i < K_1; i++) {
				var uid = distanceMatrixIndex [location, i];
				uid = idMapperLocations [uid];

				var l_features = L_1.GetRow (uid);
				var weight = W [location, i];

				l_features = DataType.VectorExtensions.Multiply (l_features, weight);
				DataType.VectorExtensions.Add (sum, l_features);
			}

			var score = DataType.VectorExtensions.ScalarProduct (u_row, sum);
			return score;
		}

		void SaveWeightedMatrix ()
		{
			U_1.Serialize (FILENAME_U1);
			U_2.Serialize (FILENAME_U2);
			L_1.Serialize (FILENAME_L1);
			W.Serialize (FILENAME_W);
		}

		void SaveDistanceMatrix ()
		{
			distanceMatrix.Serialize (FILENAME_DISTANCE_MATRIX);
			distanceMatrixIndex.Serialize (FILENAME_DISTANCE_MATRIX_IDX);
		}


	}
}
