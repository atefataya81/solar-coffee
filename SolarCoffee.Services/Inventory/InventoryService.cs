﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarCoffee.Data;
using SolarCoffee.Data.Models;
using SolarCoffee.Services.Product;

namespace SolarCoffee.Services.Inventory
{
    public class InventoryService : IInventoryService
    {

        private readonly SolarDbContext _db;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(SolarDbContext dbContext, ILogger<InventoryService> logger)
        {
            _db = dbContext;
            _logger = logger;
        }

     
        /// <summary>
        /// Gets a ProductInventory instacne by Produc tID
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public ProductInventory GetByProductId(int productId)
        {
            return _db.ProductInventories
                .Include(pi => pi.Product)
                .FirstOrDefault(pi => pi.Product.Id == productId);
        }
        /// <summary>
        /// Returns All current inventory from the database
        /// </summary>
        /// <returns></returns>
        public List<ProductInventory> GetCurrentInventory()
        {
            return _db.ProductInventories
                .Include(pi => pi.Product)
                .Where(pi => !pi.Product.IsArchived)
                .ToList();
        }
        /// <summary>
        /// Return Snapshot history for the previous 6 hours
        /// </summary>
        /// <returns></returns>
        public List<ProductInventorySnapshot> GetSnapshotHistory()
        {
            var earliest = DateTime.UtcNow - TimeSpan.FromHours(6);

            return _db.ProductInventorySnapshots
                 .Include(snap => snap.Product)
                 .Where(snap => snap.SnapshotTime > earliest
                        && !snap.Product.IsArchived
                 ).ToList() ;
        }
        /// <summary>
        /// Updates number of units available of the provided product id
        /// Adjusts QuantityOnHand by adjustment value
        /// </summary>
        /// <param name="id">productId</param>
        /// <param name="adjustment">number of units addedd / removed from inventory</param>
        /// <returns></returns>
        public ServiceResponse<ProductInventory> UpdateUnitsAvailable(int id, int adjustment)
        {
            var now = DateTime.UtcNow;

            try
            {
                var inventory = _db.ProductInventories
                    .Include(inv => inv.Product)
                    .First(inv => inv.Product.Id == id);

                inventory.QuantityOnHand += adjustment;

                try
                {
                    CreateSnapShot(inventory);
                }catch(Exception e)
                {
                    _logger.LogError("Error Creating inventory snapshot.");
                    _logger.LogError(e.StackTrace);
                    
                }
                _db.SaveChanges();

                return new ServiceResponse<ProductInventory>
                {
                    IsSuccess = true,
                    Data = inventory,
                    Message = $"Product {id} inventory adjusted",
                    Time = now
                };
            }
            catch (Exception e)
            {

                return new ServiceResponse<ProductInventory>
                {
                    IsSuccess = false,
                    Data = null,
                    Message = e.StackTrace,
                    Time = now
                };
            }
        }
        /// <summary>
        /// Create a snapshot record using the provided ProductInventory instance
        /// </summary>
        /// <param name="inventory"></param>
        private void CreateSnapShot(ProductInventory inventory)
        {
            var now = DateTime.UtcNow;
            var snapshot = new ProductInventorySnapshot
            {
                SnapshotTime = now,
                Product = inventory.Product,
                QuantityOnHand = inventory.QuantityOnHand
            };

            _db.Add(snapshot);
        }
    }
}
