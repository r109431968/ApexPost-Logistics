Feature: Parcel status tracking
  As a customer waiting for a delivery
  I want to follow a parcel as it progresses
  So that I know its current delivery status

  Background:
    Given the API is running with a clean test database
    And I am signed in as "admin"
    And a shipment "APX-30000001" already exists

  Scenario Outline: A shipment progresses through its delivery journey
    When shipment "APX-30000001" follows the journey "<journey>"
    Then shipment "APX-30000001" has status "<status>"

    Examples:
      | journey                                                    | status         |
      | PickedUp                                                   | PickedUp       |
      | PickedUp then InTransit                                    | InTransit      |
      | PickedUp then InTransit then OutForDelivery                | OutForDelivery |
      | PickedUp then InTransit then OutForDelivery then Delivered | Delivered      |
