Feature: Shipment creation
  As an administrator at an ApexPost branch
  I want to register a new shipment with a valid tracking number and weight
  So that customers and couriers can track it

  Background:
    Given the API is running with a clean test database
    And I am signed in as "admin"

  Scenario: Register a valid shipment
    When I create a shipment with tracking number "APX-20000001" and weight 2.5 kg
    Then the response status is 201
    And the shipment "APX-20000001" can be read back

  Scenario: Registering the same tracking number twice is refused
    Given a shipment "APX-20000002" already exists
    When I create a shipment with tracking number "APX-20000002" and weight 1.0 kg
    Then the response status is 409

  Scenario Outline: Invalid tracking numbers and weights are rejected
    When I create a shipment with tracking number "<tracking>" and weight <weight> kg
    Then the response status is 400

    Examples:
      | tracking     | weight | reason                         |
      | APX-1234     | 2.5    | tracking number too short      |
      | XYZ-12345678 | 2.5    | wrong prefix                   |
      | APX-20000003 | 0      | weight below the minimum       |
      | APX-20000004 | 71     | weight above the maximum       |
