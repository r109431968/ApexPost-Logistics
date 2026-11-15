Feature: Scan synchronisation
  As a courier with an unreliable phone connection
  I want to upload my scans and be able to send them again safely
  So that a dropped connection never creates duplicates or loses a scan

  Background:
    Given the API is running with a clean test database
    And I am signed in as "courier007"

  Scenario: A new scan is accepted
    When courier "courier-007" uploads an "InTransit" scan for shipment "APX-10000003"
    Then the response status is 200
    And the upload reports 1 accepted and 0 duplicates skipped

  Scenario: The same scan sent twice is stored only once
    Given courier "courier-007" has uploaded an "InTransit" scan for shipment "APX-10000003"
    When courier "courier-007" uploads the same scan again for shipment "APX-10000003"
    Then the response status is 200
    And the upload reports 0 accepted and 1 duplicates skipped

  Scenario: A courier cannot upload scans for another courier
    When courier "courier-008" uploads an "InTransit" scan for shipment "APX-10000003"
    Then the response status is 403

  Scenario Outline: A Delivered scan needs the name of the person who signed
    When courier "courier-007" uploads a "<scanType>" scan with signature name "<signature>" for shipment "APX-10000003"
    Then the response status is <status>

    Examples:
      | scanType  | signature | status |
      | Delivered |           | 400    |
      | InTransit |           | 200    |
