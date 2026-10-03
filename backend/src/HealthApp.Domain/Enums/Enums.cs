namespace HealthApp.Domain.Enums;

public enum UserRole { Customer, OutletAdmin, SuperAdmin }
public enum OutletStatus { Pending, Active, Suspended }
public enum SubscriptionStatus { Active, Paused, Cancelled }
public enum SubscriptionDeliveryMode { IndividualMealDelivery = 1, OneDeliveryPerDay = 2 }
public enum SubscriptionDuration { OneWeek = 1, TwoWeeks = 2, OneMonth = 3 }
public enum MealSelectionStatus { Scheduled = 1, Skipped = 2, Unused = 3, Rescheduled = 4, Prepared = 5, OutForDelivery = 6, Delivered = 7, Expired = 8, Cancelled = 9 }
public enum MealSlot { Morning = 1, Afternoon = 2, Evening = 3, Night = 4 }
public enum MealPortionSize { Regular = 1, Large = 2 }
public enum CreditTransactionType { Credit = 1, Debit = 2, Adjustment = 3, Refund = 4 }
public enum OrderStatus { Pending, Confirmed, Preparing, OutForDelivery, Delivered, Cancelled }
public enum DeliveryStatus { Scheduled, Preparing, OutForDelivery, Delivered, Failed, Skipped }
public enum BillingPlan { Starter, Growth, Scale }
public enum RecipeCategory { Veg, NonVeg, Vegan }

public enum CustomerGoal { WeightLoss = 1, MuscleGain = 2, GLP1Support = 3, HighPerformance = 4 }
public enum ActivityLevel { Sedentary = 1, Light = 2, Moderate = 3, High = 4, Athlete = 5 }
