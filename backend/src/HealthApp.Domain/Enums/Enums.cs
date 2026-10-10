namespace HealthApp.Domain.Enums;

public enum UserRole { Customer, OutletAdmin, SuperAdmin, Driver, OutletManager, KitchenStaff, AreaManager }
public enum OutletStatus { Pending = 0, Active = 1, Suspended = 2, Live = 3 }
public enum SubscriptionStatus { Active, Paused, Cancelled, Pending }
public enum TrialStatus { Active, Converted, Expired, Cancelled }
public enum SubscriptionDeliveryMode { IndividualMealDelivery = 1, OneDeliveryPerDay = 2 }
public enum SubscriptionDuration { OneWeek = 1, TwoWeeks = 2, OneMonth = 3, ThreeDays = 4, FiveDays = 5 }
public enum MealSelectionStatus { Scheduled = 1, Skipped = 2, Unused = 3, Rescheduled = 4, Prepared = 5, OutForDelivery = 6, Delivered = 7, Expired = 8, Cancelled = 9 }
public enum MealSlot { Morning = 1, Afternoon = 2, Evening = 3, Night = 4 }
public enum MealPortionSize { Regular = 1, Large = 2 }
public enum CreditTransactionType { Credit = 1, Debit = 2, Adjustment = 3, Refund = 4 }
public enum OrderStatus { Pending, Confirmed, Preparing, OutForDelivery, Delivered, Cancelled }
public enum DeliveryStatus { Scheduled, Preparing, OutForDelivery, Delivered, Failed, Skipped, PickedUp, FoodReady }
public enum RouteStatus { Planned, InProgress, Completed, Cancelled, Dispatched }
public enum DeliveryCoverageMode { Radius = 1, Areas = 2 }
public enum BillingPlan { Starter, Growth, Scale }
public enum GstMode { Exclusive = 0, Inclusive = 1 }
public enum OutletPackageDiscountType { None = 0, Percent = 1, Fixed = 2 }
public enum RecipeCategory { Veg, NonVeg, Vegan }
public enum OutletDomainStatus { Pending = 0, Verified = 1, Active = 2, Disabled = 3 }

public enum CustomerGoal { WeightLoss = 1, MuscleGain = 2, GLP1Support = 3, HighPerformance = 4 }
public enum ActivityLevel { Sedentary = 1, Light = 2, Moderate = 3, High = 4, Athlete = 5 }
