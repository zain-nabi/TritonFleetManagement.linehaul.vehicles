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
