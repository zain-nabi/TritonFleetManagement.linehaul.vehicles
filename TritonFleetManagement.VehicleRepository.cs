using Dapper;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Triton.Core;
using Triton.FleetManagement.WebApi.Interface;
using Triton.Service.Model.TritonFleetManagement.Custom;
using Triton.Service.Model.TritonFleetManagement.Tables;
using System.Collections.Generic;
using System;
using Dapper.Contrib.Extensions;
using System.Data;
using Triton.Service.Model.TritonFleetManagement.StoredProcs;
using Triton.Model.LeaveManagement.Tables;

namespace Triton.FleetManagement.WebApi.Repository
{
    public class BookingsRepository : IBookings
    {
        private readonly IConfiguration _config;
        public BookingsRepository(IConfiguration configuration)
        {
            _config = configuration;
        }

        public async Task<proc_BookingDetails_GetByID> CheckIfBookingExist(int CustomerID, int VehicleID, string EstimatedArrivalDate)
        {
            const string sql = "[TritonFleetManagement].[dbo].[proc_Bookings_CheckIfBookingExist]";
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            return connection.Query<proc_BookingDetails_GetByID>(sql, new { CustomerID, VehicleID, EstimatedArrivalDate }, commandType: CommandType.StoredProcedure).FirstOrDefault();
        }

        public async Task<bool> DeleteAsync(Bookings bookings)
        {
            try
            {

                await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
                bookings.DeletedOn = DateTime.Now;
                DBConnection.GetContextInformationFromConnection(connection, bookings.CreatedByUserID);
                _ = await connection.UpdateAsync(bookings);
                // Return success
                return true;
            }
            catch //(Exception exc)
            {
                return false;
            }
        }

        public async Task<List<BookingsModel>> GetAllAsync()
        {
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            var query = string.Format(@"
                                        SELECT
                                              B.BookingsID[BookingsID],B.*, 
                                        	  V.VehicleID[VehicleID],V.*,
                                        	  C.CustomerID[CustomerID],C.*
                                        FROM [TritonFleetManagement].[dbo].[Bookings] B WITH(NOLOCK)
                                        INNER JOIN  [TritonFleetManagement].[dbo].[Customer] C WITH(NOLOCK) ON C.CustomerID = B.CustomerID
                                        INNER JOIN  [TritonFleetManagement].[dbo].[Vehicle] V WITH(NOLOCK) ON V.VehicleID = B.VehicleID
                                        WHERE B.isJobCard = 0
                                        ORDER BY B.BookingsID DESC
                                             "
                                       );
            var bookModel = new List<BookingsModel>();

            var data = connection.Query<Bookings, VehiclesModel, CustomersModels, List<BookingsModel>>(
                 query, (Bookings, VehiclesModel, CustomersModels) =>
                 {
                     var model = new BookingsModel
                     {
                         Bookings = Bookings,
                         Vehicle = VehiclesModel,
                         Customer = CustomersModels
                     };

                     bookModel.Add(model);
                     return bookModel;
                 },

                 splitOn: "BookingsID,VehicleID,CustomerID").FirstOrDefault();

            return data == null ? new List<BookingsModel>() : data;
        }


        public async Task<BookingsModel> GetByIdAsync(int bookingsID)
        {
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));

            const string sql = "EXEC TritonFleetManagement.dbo.proc_Bookings_GetBookingsByID @BookingsID " +
                               "EXEC [TritonFleetManagement].[dbo].[proc_Vehicle_Select]" +
                               "EXEC [TritonFleetManagement].[dbo].[proc_Customer_Select]" +
                               "EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 83 " +
                               "EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 82 " +
                               "EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 81 " +
                               "EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 85 " +
                               "EXEC [TritonSecurity].[dbo].[proc_Branches_GetActiveBranches]" +
                               "EXEC [TritonFleetManagement].[dbo].[proc_BookingReason_GetByBookingdID] @BookingsID " +
                               "EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 86 " +
                               "EXEC [LeaveManagement].[dbo].[proc_JobProfiles_GetAllMechanics_Select]"
                               ;
            var bookingsModel = new BookingsModel();

            using (var multi = connection.QueryMultiple(sql, new { BookingsID = bookingsID }))
            {
                bookingsModel.Bookings = multi.Read<Bookings>().FirstOrDefault();
                bookingsModel.Vehicles = multi.Read<VehiclesModel>().ToList();
                bookingsModel.Customers = multi.Read<CustomersModels>().ToList();
                bookingsModel.MileAgeOrHour = multi.Read<LookupCodeModel>().ToList();
                bookingsModel.ServiceCategoryTypes = multi.Read<LookupCodeModel>().ToList();
                bookingsModel.BookingReasons = multi.Read<LookupCodeModel>().ToList();
                bookingsModel.Quotations = multi.Read<LookupCodeModel>().ToList();
                bookingsModel.Branches = multi.Read<BranchesModel>().ToList();
                bookingsModel.BookingsReasons = multi.Read<BookingReason>().ToList();
                bookingsModel.SelectedCustomer = bookingsModel.Bookings.CustomerID;
                bookingsModel.SelectedVehicles = bookingsModel.Bookings.VehicleID;
                bookingsModel.SelectedQuotations = bookingsModel.Bookings.QuotationsLCID;
                bookingsModel.SelectedMileAge = bookingsModel.Bookings.MileAge;
                bookingsModel.SelectedHour = bookingsModel.Bookings.Hour;
                bookingsModel.SelectedMileAgeOrHour = bookingsModel.Bookings.MileAgeOrHourLCID;
                bookingsModel.SelectedBranch = bookingsModel.Bookings.BranchID;
                bookingsModel.SelectedQuotations = bookingsModel.Bookings.QuotationsLCID;
                bookingsModel.MechanicTypes = multi.Read<LookupCodeModel>().ToList();
                bookingsModel.MechanicalEmployees = multi.Read<Employees>().ToList();
            }

            return bookingsModel;
        }

        public async Task<bool> InsertAsync(BookingsModel bookingsModel)
        {
            try
            {
                await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
                DBConnection.GetContextInformationFromConnection(connection, bookingsModel.Bookings.CreatedByUserID);
                bookingsModel.Bookings.CreatedOn = DateTime.Now;
                return connection.Query<bool>("[TritonFleetManagement].[dbo].[proc_Bookings_Insert_BookingsModel]",
                new
                {

                    bookingsModel.Bookings.CustomerID,
                    bookingsModel.Bookings.VehicleID,
                    bookingsModel.Bookings.ServiceCategoryTypesLCID,
                    bookingsModel.Bookings.MileAgeOrHourLCID,
                    bookingsModel.Bookings.MileAge,
                    bookingsModel.Bookings.Hour,
                    bookingsModel.Bookings.EstimatedArrival,
                    bookingsModel.Bookings.Authorisation,
                    bookingsModel.Bookings.Notes,
                    bookingsModel.Bookings.CreatedOn,
                    bookingsModel.Bookings.CreatedByUserID,
                    bookingsModel.Bookings.QuotationsLCID,
                    bookingsModel.Bookings.BranchID,
                    bookingsModel.Bookings.OrderNumber,
                    bookingsModel.Bookings.StatusLCID,
                    bookingsModel.BookingReasonLCID

                },
                commandType: CommandType.StoredProcedure).FirstOrDefault();
            }
            catch //(Exception e)
            {
                return false;
            }
        }

        public async Task<BookingsModel> LookUpCodesAsync(int customerID)
        {
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            var bookingModel = new BookingsModel();

            var query = string.Format(@" 
                                        EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 81
                                        EXEC [TritonFleetManagement].[dbo].[proc_Vehicle_GetVehicleByCustomerID] @CustomerID
                                        EXEC [TritonFleetManagement].[dbo].[proc_Customer_Select]
                                        EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 83
                                        EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 82
                                        EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 85
                                        EXEC [TritonSecurity].[dbo].[proc_Branches_GetActiveBranches]
                                        EXEC [TritonGroup].[dbo].[proc_LookUpCodes_ByCategoryID_Select] 86
                                        "
                                     );
            using (var multi = connection.QueryMultiple(query, new { CustomerID = customerID }))
            {
                bookingModel.BookingReasons = multi.Read<LookupCodeModel>().ToList();
                bookingModel.Vehicles = multi.Read<VehiclesModel>().ToList();
                bookingModel.Customers = multi.Read<CustomersModels>().ToList();
                bookingModel.MileAgeOrHour = multi.Read<LookupCodeModel>().ToList();
                bookingModel.ServiceCategoryTypes = multi.Read<LookupCodeModel>().ToList();
                bookingModel.Quotations = multi.Read<LookupCodeModel>().ToList();
                bookingModel.Branches = multi.Read<BranchesModel>().ToList();
                bookingModel.MechanicTypes = multi.Read<LookupCodeModel>().ToList();
                bookingModel.Bookings = new Bookings();

            }
            return bookingModel;
        }

        public async Task<bool> UpdateAsync(BookingsModel bookingsModel)
        {
            try
            {
                await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
                DBConnection.GetContextInformationFromConnection(connection, bookingsModel.Bookings.CreatedByUserID);
                bookingsModel.Bookings.CreatedOn = DateTime.Now;
                return connection.Query<bool>("[TritonFleetManagement].[dbo].[proc_Bookings_Update_BookingsModel]",
                new
                {
                    bookingsModel.Bookings.BookingsID,
                    bookingsModel.Bookings.CustomerID,
                    bookingsModel.Bookings.VehicleID,
                    bookingsModel.Bookings.ServiceCategoryTypesLCID,
                    bookingsModel.Bookings.MileAgeOrHourLCID,
                    bookingsModel.Bookings.MileAge,
                    bookingsModel.Bookings.Hour,
                    bookingsModel.Bookings.EstimatedArrival,
                    bookingsModel.Bookings.ActualArrival,
                    bookingsModel.Bookings.Authorisation,
                    bookingsModel.Bookings.Notes,
                    bookingsModel.Bookings.CreatedByUserID,
                    bookingsModel.Bookings.QuotationsLCID,
                    bookingsModel.Bookings.BranchID,
                    bookingsModel.Bookings.OrderNumber,
                    bookingsModel.Bookings.isJobCard,
                    bookingsModel.Bookings.StatusLCID,
                    bookingsModel.Bookings.MechanicEmployeeID,
                    bookingsModel.BookingReasonLCID,
                    bookingsModel.DeleteBookingReasonLCID

                },
                commandType: CommandType.StoredProcedure).FirstOrDefault();
            }
            catch //(Exception e)
            {
                return false;
            }
        }

        public async Task<List<BookingsModel>> GetBookingsByEstimatedDateAsync(DateTime estimatedArrivalDateFrom, DateTime estimatedArrivalDateTo)
        {
            //estimatedArrivalDate = Convert.ToDateTime("2021-04-30");
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            var query = string.Format(@"
                                        SELECT
                                              B.BookingsID[BookingsID],B.*, 
                                        	  V.VehicleID[VehicleID],V.*,
                                        	  C.CustomerID[CustomerID],C.*
                                        FROM [TritonFleetManagement].[dbo].[Bookings] B WITH(NOLOCK)
                                        INNER JOIN  [TritonFleetManagement].[dbo].[Customer] C WITH(NOLOCK) ON C.CustomerID = B.CustomerID
                                        INNER JOIN  [TritonFleetManagement].[dbo].[Vehicle] V WITH(NOLOCK) ON V.VehicleID = B.VehicleID
										WHERE B.EstimatedArrival >= @estimatedArrivalDateFrom and B.EstimatedArrival <= @estimatedArrivalDateTo
                                        ORDER BY B.BookingsID DESC
                                             "
                                       );


            var bookModel = new List<BookingsModel>();

            var data = connection.Query<Bookings, VehiclesModel, CustomersModels, List<BookingsModel>>(
                 query, (Bookings, VehiclesModel, CustomersModels) =>
                 {
                     var model = new BookingsModel
                     {
                         Bookings = Bookings,
                         Vehicle = VehiclesModel,
                         Customer = CustomersModels
                     };

                     bookModel.Add(model);
                     return bookModel;
                 },
                 new { estimatedArrivalDateFrom, estimatedArrivalDateTo },
                 splitOn: "BookingsID,VehicleID,CustomerID").FirstOrDefault();

            return data == null ? new List<BookingsModel>() : data;
        }


        public async Task<List<proc_VendorCodes_String_Agg>> GetVendorCodesPerCustomer()
        {
            const string sql = "[TritonFleetManagement].[dbo].[proc_VendorCodes_String_Agg]";
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            return connection.Query<proc_VendorCodes_String_Agg>(sql, commandType: CommandType.StoredProcedure).ToList();
        }

        public async Task<List<CustomersModels>> GetAllCustomersAsync()
        {
            const string sql = "[TritonFleetManagement].[dbo].[proc_Customer_Select]";
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            return connection.Query<CustomersModels>(sql, commandType: CommandType.StoredProcedure).ToList();
        }

        public async Task<List<Employees>> GetAllMechanics()
        {
            const string sql = "[LeaveManagement].[dbo].[proc_JobProfiles_GetAllMechanics_Select]";
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.LeaveManagement));
            return connection.Query<Employees>(sql, commandType: CommandType.StoredProcedure).ToList();
        }

        public async Task<List<proc_Bookings_BookingReasons_Customers_Select>> GetBookingsPerCustomer(int CustomerID, DateTime startDate, DateTime endDate)
        {
            const string sql = "[TritonFleetManagement].[dbo].[proc_Bookings_BookingReasons_Customers_Select]";
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            return connection.Query<proc_Bookings_BookingReasons_Customers_Select>(sql, new { CustomerID, startDate, endDate }, commandType: CommandType.StoredProcedure).ToList();
        }

        public async Task<proc_BookingDetails_GetByID> GetBookingDetailsByID(int BookingsID)
        {
            const string sql = "[TritonFleetManagement].[dbo].[proc_BookingDetails_GetByID]";
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            return connection.Query<proc_BookingDetails_GetByID>(sql, new { BookingsID }, commandType: CommandType.StoredProcedure).FirstOrDefault();
        }

        public async Task<bool> DeleteBooking(proc_BookingDetails_GetByID model)
        {
            try
            {
                await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
                DBConnection.GetContextInformationFromConnection(connection, model.DeletedByUserID);
                _ = connection.Query<bool>("[TritonFleetManagement].[dbo].[proc_Bookings_Delete]",
                new
                {
                    model.BookingsID,
                    model.DeletedByUserID
                },
                commandType: CommandType.StoredProcedure).FirstOrDefault();

                return true;
            }
            catch //(Exception e)
            {
                return false;
            }
        }

        public async Task<bool> InsertDocumentRepositoryAsync(DocumentRepository documentRepository, int bookingID)
        {
            try
            {
                await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));

                return connection.Query<bool>("[TritonFleetManagement].[dbo].[proc_Bookings_Insert_DocumentRepository]",
                new
                {
                    //documentRepository.DocumentRepositoryID,
                    documentRepository.ImgName,
                    documentRepository.ImgData,
                    documentRepository.ImgContentType,
                    documentRepository.ImgLength,
                    bookingID,
                    documentRepository.CreatedByUserID,
                },
                commandType: CommandType.StoredProcedure).FirstOrDefault();
            }
            catch (Exception)
            {
                //throw e;
                return false;
            }
        }

        public async Task<List<DocumentVehicleModel>> GetAllDocuments(int bookingId)
        {
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));
            var query = string.Format(@"
                                        	SELECT DR.ImgName, DR.ImgData, DR.ImgContentType, DR.ImgLength, VD.VehicleDocumentID, VD.BookingID
	                                        FROM [TritonFleetManagement].[dbo].[DocumentRepository] DR
											INNER JOIN [TritonFleetManagement].[dbo].[VehicleDocument] VD ON VD.DocumentRepositoryID = DR.DocumentRepositoryID
											WHERE VD.BookingID= @bookingId AND VD.DeletedOn IS NULL 
                                            ORDER BY DR.DocumentRepositoryID DESC"
                                     );

            var documents = new List<DocumentRepository>();
            var documentRepositories = connection.Query<DocumentVehicleModel>(query, new { bookingId }).ToList();
            return documentRepositories;
        }

        public async Task<bool> DeleteDocument(int vehicleDocumentID, int deletedByUserID)
        {
            try
            {
                await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));

                return connection.Query<bool>("[TritonFleetManagement].[dbo].[proc_Bookings_Delete_DocumentRepository]",
                new
                {
                    vehicleDocumentID,
                    deletedByUserID
                },
                commandType: CommandType.StoredProcedure).FirstOrDefault();
            }
            catch (Exception)
            {
                //throw e;
                return false;
            }
        }

        public async Task<List<BookingAuditModel>> GetBookingAuditAsync(int BookingID)
        {
            await using var connection = DBConnection.GetOpenConnection(_config.GetConnectionString(StringHelpers.Database.TritonFleetManagement));

            return connection.Query<BookingAuditModel>("proc_BookingAudit_Select", new { bookingID = BookingID }, commandType: CommandType.StoredProcedure).ToList();
        }
    }
}