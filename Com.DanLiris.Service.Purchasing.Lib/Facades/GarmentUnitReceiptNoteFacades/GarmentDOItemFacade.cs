﻿using AutoMapper;
using Com.DanLiris.Service.Purchasing.Lib.Helpers;
using Com.DanLiris.Service.Purchasing.Lib.Interfaces;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentDeliveryOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentExternalPurchaseOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentInternalPurchaseOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentInventoryModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentUnitDeliveryOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentUnitExpenditureNoteModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentUnitReceiptNoteModel;
using Com.DanLiris.Service.Purchasing.Lib.Services;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.GarmentUnitReceiptNoteViewModels;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Com.Moonlay.Models;
using System.Globalization;
using Com.Moonlay.NetCore.Lib;
using System.IO;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.GarmentUnitReceiptNoteViewModels.DOItems;
using System.Data;
using OfficeOpenXml;
using Com.DanLiris.Service.Purchasing.Lib.PDFTemplates.GarmentUnitReceiptNotePDFTemplates;

namespace Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentUnitReceiptNoteFacades
{
    public class GarmentDOItemFacade : IGarmentDOItemFacade
    {
        private readonly string USER_AGENT = "Facade";

        private readonly IServiceProvider serviceProvider;
        private readonly IdentityService identityService;

        private readonly DbSet<GarmentDOItems> dbSetGarmentDOItems;
        private readonly DbSet<GarmentUnitReceiptNote> dbSetGarmentUnitReceiptNote;
        private readonly DbSet<GarmentUnitReceiptNoteItem> dbSetGarmentUnitReceiptNoteItem;
        private readonly DbSet<GarmentExternalPurchaseOrderItem> dbSetGarmentExternalPurchaseOrderItem;
        private readonly DbSet<GarmentUnitExpenditureNote> garmentUnitExpenditureNotes;
        private readonly DbSet<GarmentUnitExpenditureNoteItem> garmentUnitExpenditureNoteItems;
        private readonly DbSet<GarmentUnitDeliveryOrderItem> garmentUnitDeliveryOrderItems;
        private readonly DbSet<GarmentUnitDeliveryOrder> garmentUnitDeliveryOrders;
        private readonly PurchasingDbContext dbContext;

        public GarmentDOItemFacade(IServiceProvider serviceProvider, PurchasingDbContext dbContext)
        {
            this.serviceProvider = serviceProvider;
            identityService = (IdentityService)serviceProvider.GetService(typeof(IdentityService));

            dbSetGarmentDOItems = dbContext.Set<GarmentDOItems>();
            dbSetGarmentUnitReceiptNote = dbContext.Set<GarmentUnitReceiptNote>();
            dbSetGarmentUnitReceiptNoteItem = dbContext.Set<GarmentUnitReceiptNoteItem>();
            dbSetGarmentExternalPurchaseOrderItem = dbContext.Set<GarmentExternalPurchaseOrderItem>();
            garmentUnitExpenditureNotes = dbContext.Set<GarmentUnitExpenditureNote>();
            garmentUnitExpenditureNoteItems = dbContext.Set<GarmentUnitExpenditureNoteItem>();
            garmentUnitDeliveryOrderItems = dbContext.Set<GarmentUnitDeliveryOrderItem>();
            garmentUnitDeliveryOrders = dbContext.Set<GarmentUnitDeliveryOrder>();
            this.dbContext = dbContext;
        }

        public List<object> ReadForUnitDO(string Keyword = null, string Filter = "{}")
        {
            var GarmentDOItemsQuery = dbSetGarmentDOItems.Where(entity=>entity.RemainingQuantity>0).Select(a=> new { a.Id, a.EPOItemId, a.URNItemId, a.UnitId, a.StorageId, a.RO, a.POSerialNumber});
            IQueryable<GarmentUnitReceiptNoteItem> GarmentUnitReceiptNoteItemsQuery = dbSetGarmentUnitReceiptNoteItem;
            //IQueryable<GarmentUnitReceiptNote> GarmentUnitReceiptNotesQuery = dbSetGarmentUnitReceiptNote;
            //IQueryable<GarmentExternalPurchaseOrderItem> GarmentExternalPurchaseOrderItemsQuery = dbSetGarmentExternalPurchaseOrderItem;

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            long unitId = 0;
            long storageId = 0;
            long doItemsId = 0;
            bool hasUnitFilter = FilterDictionary.ContainsKey("UnitId") && long.TryParse(FilterDictionary["UnitId"], out unitId);
            bool hasStorageFilter = FilterDictionary.ContainsKey("StorageId") && long.TryParse(FilterDictionary["StorageId"], out storageId);
            bool hasRONoFilter = FilterDictionary.ContainsKey("RONo");
            bool hasPOSerialNumberFilter = FilterDictionary.ContainsKey("POSerialNumber");
            string RONo = hasRONoFilter ? (FilterDictionary["RONo"] ?? "").Trim() : "";
            string POSerialNumber = hasPOSerialNumberFilter ? (FilterDictionary["POSerialNumber"] ?? "").Trim() : "";
            bool hasDOItemIdFilter = FilterDictionary.ContainsKey("DOItemsId") && long.TryParse(FilterDictionary["DOItemsId"], out doItemsId);

            if (hasDOItemIdFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.Id == doItemsId);
            }
            else
            {
                if (hasUnitFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.UnitId == unitId);
                }
                if (hasStorageFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.StorageId == storageId);
                }
                if (hasRONoFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.RO == RONo);
                }
                if (hasPOSerialNumberFilter)
                {
                    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.POSerialNumber == POSerialNumber);
                }
            }
            

            List<object> ListData = new List<object>();
            var data = (from doi in GarmentDOItemsQuery
                       join urni in GarmentUnitReceiptNoteItemsQuery.IgnoreQueryFilters() on doi.URNItemId equals urni.Id
                       where  (urni.IsDeleted == true && urni.DeletedAgent=="LUCIA") || (urni.IsDeleted == false) 
                       select new
                       {
                           DOItemsId = doi.Id,
                           urni.URNId,
                           doi.URNItemId,
                           doi.EPOItemId,
                       }).OrderBy(x => x.EPOItemId);
            var urnIds= data.Select(s => s.URNId).ToList().Distinct().ToList();
            var URNs = dbSetGarmentUnitReceiptNote.IgnoreQueryFilters().Where(u => urnIds.Contains(u.Id))
                .Select(s => new { s.Id, s.URNNo}).ToList();
            var urnItemIds = data.Select(s => s.URNItemId).ToList().Distinct().ToList();
            var urnItems = dbSetGarmentUnitReceiptNoteItem.IgnoreQueryFilters().Where(w => urnItemIds.Contains(w.Id))
                .Select(s => new { s.Id, s.DODetailId, s.ProductRemark, s.PricePerDealUnit, s.ReceiptCorrection, s.CorrectionConversion }).ToList();

            var epoItemIds = data.Select(s => s.EPOItemId).ToList().Distinct().ToList();
            var epoItems = dbSetGarmentExternalPurchaseOrderItem.IgnoreQueryFilters().Where(w => epoItemIds.Contains(w.Id))
                .Select(s => new { s.Id, s.Article }).ToList().ToList();

            var DOItemIds = data.Select(s => s.DOItemsId).Distinct().ToList();
            var DOItems = dbSetGarmentDOItems.Where(w => DOItemIds.Contains(w.Id))
                .Select(s => new
                {
                    DOItemsId = s.Id,
                    s.POItemId,
                    s.URNItemId,
                    s.EPOItemId,
                    s.PRItemId,
                    s.ProductId,
                    s.ProductCode,
                    s.ProductName,
                    s.SmallQuantity,
                    s.SmallUomId,
                    s.SmallUomUnit,
                    s.DesignColor,
                    s.POSerialNumber,
                    s.RemainingQuantity,
                    s.Colour,
                    s.Rack,
                    s.Box,
                    s.Level,
                    s.Area,
                    RONo = s.RO
                }).ToList();
            foreach (var item in data)
            {
                var urn = URNs.FirstOrDefault(f => f.Id.Equals(item.URNId));
                var urnItem = urnItems.FirstOrDefault(f => f.Id.Equals(item.URNItemId));
                var epoItem = epoItems.FirstOrDefault(f => f.Id.Equals(item.EPOItemId));
                var doItem = DOItems.FirstOrDefault(f => f.DOItemsId.Equals(item.DOItemsId));

                ListData.Add(new
                {
                    doItem.DOItemsId,
                    URNId = urn.Id,
                    urn.URNNo,
                    doItem.POItemId,
                    doItem.URNItemId,
                    doItem.EPOItemId,
                    doItem.PRItemId,
                    doItem.ProductId,
                    doItem.ProductCode,
                    doItem.ProductName,
                    doItem.SmallQuantity,
                    doItem.SmallUomId,
                    doItem.SmallUomUnit,
                    doItem.DesignColor,
                    doItem.POSerialNumber,
                    doItem.RemainingQuantity,
                    doItem.RONo,
                    doItem.Colour,
                    doItem.Rack,
                    doItem.Level,
                    doItem.Box,
                    doItem.Area,
                    epoItem.Article,
                    urnItem.DODetailId,
                    urnItem.ProductRemark,
                    urnItem.PricePerDealUnit,
                    urnItem.ReceiptCorrection,
                    urnItem.CorrectionConversion
                });
            }

            return ListData;
        }

        public List<object> ReadForUnitDOMore(string Keyword = null, string Filter = "{}", int size = 50)
        {
            IQueryable<GarmentDOItems> GarmentDOItemsQuery = dbSetGarmentDOItems.Where(w => w.IsDeleted == false);
            
            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            long unitId = 0;
            long storageId = 0;

            bool hasUnitFilter = FilterDictionary.ContainsKey("UnitId") && long.TryParse(FilterDictionary["UnitId"], out unitId);
            bool hasStorageFilter = FilterDictionary.ContainsKey("StorageId") && long.TryParse(FilterDictionary["StorageId"], out storageId);
            bool hasRONoFilter = FilterDictionary.ContainsKey("RONo");
            string RONo = hasRONoFilter ? (FilterDictionary["RONo"] ?? "").Trim() : "";
            //string storageName = (FilterDictionary["StorageName"]);

            //if (storageName == "GUDANG BAHAN BAKU")
            //{
            //    GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.Colour != null);
            //}
            if (hasUnitFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.UnitId == unitId);
            }
            if (hasStorageFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.StorageId == storageId);
            }
            if (hasRONoFilter)
            {
                GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.RO != RONo);
            }
           

            Keyword = (Keyword ?? "").Trim();
            //GarmentDOItemsQuery = GarmentDOItemsQuery.Where(x => x.RemainingQuantity > 0 && (x.RO.Contains(Keyword) || x.POSerialNumber.Contains(Keyword)));

            var data = from doi in GarmentDOItemsQuery
                       where doi.RemainingQuantity>0
                       && (doi.RO.Contains(Keyword) || doi.POSerialNumber.Contains(Keyword))
                       select new
                       {
                           doi.URNItemId,
                           RONo = doi.RO,
                           doi.ProductName,
                           doi.ProductCode,
                           doi.POSerialNumber,
                           doi.RemainingQuantity,
                           DOItemsId= doi.Id,
                           doi.Colour,
                           //doi.Rack,
                           //doi.Level,
                           //doi.Box,
                           //doi.Area,
                       };

            List<object> ListData = new List<object>(data.OrderBy(o => o.RONo).Take(size));
            return ListData;
        }

        public List<DOItemsViewModels> GetByPO(string productcode, string po, string unitcode)
        {
            IQueryable<GarmentDOItems> Query = dbSetGarmentDOItems
                .Where(w => w.IsDeleted == false 
                && w.RemainingQuantity > 0 
                && w.POSerialNumber == (string.IsNullOrWhiteSpace(po) ? w.POSerialNumber : po)
                && w.UnitCode == (string.IsNullOrWhiteSpace(unitcode) ? w.UnitCode : unitcode)
                && w.ProductCode == (string.IsNullOrWhiteSpace(productcode) ? w.ProductCode : productcode)
                && w.ProductName == "FABRIC"
                );

            var data = Query.Select(x => new DOItemsViewModels
            {
                Id =  x.Id,
                POSerialNumber = x.POSerialNumber,
                RO = x.RO,
                UnitName = x.UnitName,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                RemainingQuantity = x.RemainingQuantity,
                SmallUomUnit = x.SmallUomUnit,
                Colour = x.Colour,
                Rack = x.Rack,
                Level = x.Level,
                Box = x.Box,
                Area = x.Area,
            }).ToList();
            //List<object> ListData = new List<object>(data.OrderBy(o => o.POSerialNumber));

            return data;

        }
        public GarmentDOItems ReadById(int id)
        {
            var model = dbSetGarmentDOItems.Where(m => m.Id == id)
                            .FirstOrDefault();

            return model;
        }
        public async Task<int> Update(int id, DOItemsRackingViewModels viewModels)
        {
            int Updated = 0;
            GarmentDOItems dataToCreate = new GarmentDOItems();
            using (var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    for(int a = 0; a < viewModels.Items.Count(); a++)
                    {
                        if(a == 0)
                        {
                            var data = dbSetGarmentDOItems.Where(x => x.Id == id).FirstOrDefault();
                            EntityExtension.FlagForUpdate(data, identityService.Username, USER_AGENT);
                            dataToCreate = data;
                            data.RemainingQuantity = viewModels.Items[a].Quantity;
                            data.Colour = viewModels.Items[a].Colour.ToUpper();
                            data.Rack = viewModels.Items[a].Rack.ToUpper();
                            data.Box = viewModels.Items[a].Box.ToUpper();
                            data.Level = viewModels.Items[a].Level.ToUpper();
                            data.Area = viewModels.Items[a].Area.ToUpper();
                            data.SplitQuantity = viewModels.Items[a].Quantity;


                            await dbContext.SaveChangesAsync();

                        }
                        else 
                        {
                            GarmentDOItems garmentDOItems = new GarmentDOItems
                            {
                                DOItemNo = dataToCreate.DOItemNo,
                                UnitId = dataToCreate.UnitId,
                                UnitCode = dataToCreate.UnitCode,
                                UnitName = dataToCreate.UnitName,
                                StorageCode = dataToCreate.StorageCode,
                                StorageId = dataToCreate.StorageId,
                                StorageName = dataToCreate.StorageName,
                                POId = dataToCreate.POId,
                                POItemId = dataToCreate.POItemId,
                                POSerialNumber = dataToCreate.POSerialNumber,
                                ProductCode = dataToCreate.ProductCode,
                                ProductId = dataToCreate.ProductId,
                                ProductName = dataToCreate.ProductName,
                                DesignColor = dataToCreate.DesignColor,
                                SmallQuantity = dataToCreate.SmallQuantity,
                                SmallUomId = dataToCreate.SmallUomId,
                                SmallUomUnit = dataToCreate.SmallUomUnit,
                                DetailReferenceId = dataToCreate.DetailReferenceId,
                                URNItemId = dataToCreate.URNItemId,
                                DOCurrencyRate = dataToCreate.DOCurrencyRate,
                                EPOItemId = dataToCreate.EPOItemId,
                                PRItemId = dataToCreate.PRItemId,
                                RO = dataToCreate.RO,

                                RemainingQuantity = viewModels.Items[a].Quantity,
                                Colour = viewModels.Items[a].Colour.ToUpper(),
                                Rack = viewModels.Items[a].Rack.ToUpper(),
                                Box = viewModels.Items[a].Box.ToUpper(),
                                Level = viewModels.Items[a].Level.ToUpper(),
                                Area = viewModels.Items[a].Area.ToUpper(),
                                SplitQuantity = viewModels.Items[a].Quantity,
                            };

                            EntityExtension.FlagForCreate(garmentDOItems, identityService.Username, USER_AGENT);
                            //dataToCreate.RemainingQuantity = viewModels.Items[a].Quantity;
                            dbSetGarmentDOItems.Add(garmentDOItems);
                            await dbContext.SaveChangesAsync();
                        }
                    }

                    Updated = await dbContext.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }

            }
            return Updated;
        }

        public List<StellingEndViewModels> GetStellingQuery(int id,int offset)
        {
            var QueryReceipt = (from a in dbSetGarmentDOItems
                                where a.Id.Equals(id) && a.IsDeleted == false 
                                select new StellingViewModels
                                {
                                    POSerialNumber = a.POSerialNumber,
                                    Quantity = a.SplitQuantity,
                                    Uom = a.SmallUomUnit,
                                    Colour = a.Colour,
                                    Rack = a.Rack,
                                    Box = a.Box,
                                    Level = a.Level,
                                    Area = a.Area,
                                    ReceiptDate = a.CreatedUtc,
                                    //QuantityReceipt = 
                                    ExpenditureDate = null,
                                    QtyExpenditure = null,
                                    Remaining = null,
                                    Remark = null,
                                    User = a.CreatedBy
                                }).GroupBy(x => new { x.POSerialNumber, x.Uom, x.Colour, x.Rack, x.Level, x.Box, x.Area, x.ReceiptDate, x.ExpenditureDate, x.QtyExpenditure, x.Remaining, x.Remark, x.User }, (key, group) => new StellingViewModels
                                {
                                    POSerialNumber = key.POSerialNumber,
                                    Quantity = group.Sum(x => x.Quantity),
                                    Uom = key.Uom,
                                    Colour = key.Colour,
                                    Rack = key.Rack,
                                    Box = key.Box,
                                    Level = key.Level,
                                    Area = key.Area,
                                    ReceiptDate = key.ReceiptDate,
                                    //QuantityReceipt = 
                                    ExpenditureDate = key.ExpenditureDate,
                                    QtyExpenditure = key.QtyExpenditure,
                                    Remaining = key.Remaining,
                                    Remark = key.Remark,
                                    User = key.User
                                });


            var QueryExpend = (from a in dbSetGarmentDOItems
                               join d in garmentUnitDeliveryOrderItems on a.Id equals d.DOItemsId
                               join e in garmentUnitDeliveryOrders on d.UnitDOId equals e.Id
                               join c in garmentUnitExpenditureNotes on e.Id equals c.UnitDOId
                               join b in garmentUnitExpenditureNoteItems on c.Id equals b.UENId
                               
                               where a.Id.Equals(id) && c.UnitSenderCode == a.UnitCode
                                && a.IsDeleted == false && b.IsDeleted == false
                                && c.IsDeleted == false && d.IsDeleted == false
                                && e.IsDeleted == false
                               select new StellingViewModels
                               {
                                   POSerialNumber = null,
                                   Quantity = null,
                                   Uom = null,
                                   Colour = null,
                                   Rack = null,
                                   Box = null,
                                   Level = null,
                                   Area = null,
                                   ReceiptDate = null,
                                   //QuantityReceipt = 
                                   ExpenditureDate = c.CreatedUtc,
                                   QtyExpenditure = b.Quantity,
                                   Remaining = null,
                                   Remark = e.RONo,
                                   User = c.CreatedBy
                               });

            var data = QueryReceipt.Union(QueryExpend).ToList();

            List<StellingEndViewModels> result = new List<StellingEndViewModels>();
            double TempQty = 0;
            foreach (var a in data)
            {
                if(a.QtyExpenditure == null)
                {
                    StellingEndViewModels stelling = new StellingEndViewModels
                    {
                        POSerialNumber = a.POSerialNumber,
                        Quantity = a.Quantity,
                        Uom = a.Uom,
                        Colour = a.Colour,
                        Rack = a.Rack,
                        Box = a.Box,
                        Level = a.Level,
                        Area = a.Area,
                        ReceiptDate = a.ReceiptDate == null ? "": a.ReceiptDate.Value.ToString("dd MMM yyyy", new CultureInfo("id-ID")),
                        ExpenditureDate = a.ExpenditureDate == null ? "" : a.ExpenditureDate.Value.ToString("dd MMM yyyy", new CultureInfo("id-ID")),
                        QtyExpenditure = a.QtyExpenditure,
                        Remaining = a.Remaining,
                        Remark = a.Remark,
                        User = a.User,
                    };

                    TempQty = (double)a.Quantity;
                    result.Add(stelling);
                }
                else
                {
                    StellingEndViewModels stelling = new StellingEndViewModels
                    {
                        POSerialNumber = a.POSerialNumber,
                        Quantity = a.Quantity,
                        Uom = a.Uom,
                        Colour = a.Colour,
                        Rack = a.Rack,
                        Box = a.Box,
                        Level = a.Level,
                        Area = a.Area,
                        ReceiptDate = a.ReceiptDate == null ? "" : a.ReceiptDate.Value.AddHours(offset).ToString("dd MMM yyyy", new CultureInfo("id-ID")),
                        ExpenditureDate = a.ExpenditureDate == null ? "" : a.ExpenditureDate.Value.AddHours(offset).ToString("dd MMM yyyy", new CultureInfo("id-ID")),
                        QtyExpenditure = a.QtyExpenditure,
                        Remaining = TempQty - a.QtyExpenditure,
                        Remark = a.Remark,
                        User = a.User
                    };

                    TempQty -= (double)a.QtyExpenditure;

                    result.Add(stelling);
                }
            }

            return result;

        }

        public MemoryStream GeneratePdf(List<StellingEndViewModels> stellingEndViewModels)
        {
            return DOItemsStellingPDFTemplate.GeneratePdfTemplate(serviceProvider, stellingEndViewModels);
        }

        public MemoryStream GenerateExcel(string productcode, string po, string unitcode)
        {
            var Query = GetByPO(productcode, po, unitcode);

            DataTable result = new DataTable();

            result.Columns.Add(new DataColumn() { ColumnName = "No", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Kode Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nomor PO", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nomor RO", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Unit", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Nama Barang", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Quantity", DataType = typeof(decimal) });
            result.Columns.Add(new DataColumn() { ColumnName = "Satuan", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Warna", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Rak", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Level", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Box", DataType = typeof(String) });
            result.Columns.Add(new DataColumn() { ColumnName = "Area", DataType = typeof(String) });

            if (Query.ToArray().Count() == 0)
                result.Rows.Add("","","","","","",0,"","","","","",""); // to allow column name to be generated properly for empty data as template
            else
            {
                int index = 1;
                foreach (var item in Query)
                {
                    result.Rows.Add(index++, item.ProductCode, item.POSerialNumber, item.RO, item.UnitName, item.ProductName, item.RemainingQuantity,item.SmallUomUnit, item.Colour,
                        item.Rack, item.Level, item.Box, item.Area);
                }
            }

            ExcelPackage package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Report");
            sheet.Cells["A1"].LoadFromDataTable(result, true, OfficeOpenXml.Table.TableStyles.Light16);

            MemoryStream stream = new MemoryStream();
            package.SaveAs(stream);
            return stream;
        }

    }
}
